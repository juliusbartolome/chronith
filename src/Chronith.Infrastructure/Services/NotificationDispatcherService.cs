using System.Text.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Telemetry;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Services;

public sealed class NotificationDispatcherService(
    IServiceScopeFactory scopeFactory,
    NotificationChannelFactory channelFactory,
    ITemplateRenderer templateRenderer,
    IOptions<NotificationDispatcherOptions> options,
    IBackgroundServiceHealthTracker healthTracker,
    ILogger<NotificationDispatcherService> logger)
    : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
                healthTracker.RecordSuccess(nameof(NotificationDispatcherService));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during notification dispatch batch");
            }
            await Task.Delay(
                TimeSpan.FromSeconds(options.Value.DispatchIntervalSeconds), stoppingToken);
        }
    }

    internal async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IWebhookOutboxRepository>();
        var configRepo = scope.ServiceProvider.GetRequiredService<INotificationConfigRepository>();
        var templateRepo = scope.ServiceProvider.GetRequiredService<INotificationTemplateRepository>();

        var pending = await outboxRepo.GetPendingByCategoryAsync(
            OutboxCategory.Notification, 50, ct);

        if (pending.Count == 0) return;

        foreach (var entry in pending)
        {
            await DispatchNotificationAsync(entry, configRepo, templateRepo, outboxRepo, ct);
        }
    }

    private async Task DispatchNotificationAsync(
        PendingOutboxEntry entry,
        INotificationConfigRepository configRepo,
        INotificationTemplateRepository templateRepo,
        IWebhookOutboxRepository outboxRepo,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // EventType format: "notification.booking_confirmed.email"
        var channelType = ExtractChannelType(entry.EventType);
        if (channelType is null)
        {
            logger.LogWarning(
                "Cannot parse channel type from EventType '{EventType}' for entry {EntryId} — marking abandoned",
                entry.EventType, entry.Id);
            await outboxRepo.MarkAbandonedAsync(entry.Id, ct);
            return;
        }

        using var activity = ChronithActivitySource.StartNotificationDispatch(entry.TenantId, channelType);

        var channel = channelFactory.GetChannel(channelType);
        if (channel is null)
        {
            logger.LogWarning(
                "Notification channel '{ChannelType}' not found for entry {EntryId} — marking abandoned",
                channelType, entry.Id);
            await outboxRepo.MarkAbandonedAsync(entry.Id, ct);
            return;
        }

        var config = await configRepo.GetByChannelTypeAsync(entry.TenantId, channelType, ct);
        if (config is null || !config.IsEnabled)
        {
            logger.LogInformation(
                "Notification channel '{ChannelType}' disabled for tenant {TenantId} — marking abandoned",
                channelType, entry.TenantId);
            await outboxRepo.MarkAbandonedAsync(entry.Id, ct);
            return;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<JsonElement>(entry.Payload, JsonOptions);
            var customerEmail = payload.GetProperty("customerEmail").GetString() ?? string.Empty;
            var eventName = payload.GetProperty("event").GetString() ?? "notification";
            var status = payload.GetProperty("status").GetString() ?? string.Empty;
            var bookingTypeSlug = payload.GetProperty("bookingTypeSlug").GetString() ?? string.Empty;

            // Build context dictionary for template rendering
            var context = new Dictionary<string, string>
            {
                ["customer_name"] = TryGetStringProperty(payload, "customerName") ?? customerEmail,
                ["customer_email"] = customerEmail,
                ["booking_type_slug"] = bookingTypeSlug,
                ["status"] = status,
                ["event_type"] = eventName,
                ["booking_id"] = TryGetStringProperty(payload, "bookingId") ?? string.Empty,
                ["booking_date"] = TryGetStringProperty(payload, "bookingDate") ?? string.Empty,
                ["booking_time"] = TryGetStringProperty(payload, "bookingTime") ?? string.Empty,
                ["staff_name"] = TryGetStringProperty(payload, "staffName") ?? string.Empty,
                ["tenant_name"] = TryGetStringProperty(payload, "tenantName") ?? string.Empty,
                ["expiry_hours"] = TryGetStringProperty(payload, "expiryHours") ?? string.Empty,
            };

            // Look up template for this event + channel type
            var template = await templateRepo.GetByEventAndChannelAsync(
                entry.TenantId, eventName, channelType, ct);

            string subject;
            string body;

            if (template is not null)
            {
                subject = templateRenderer.Render(template.Subject ?? $"Booking {status} — {bookingTypeSlug}", context);
                body = templateRenderer.Render(template.Body, context);
            }
            else
            {
                // Fallback when no template is configured
                subject = $"Booking {status} — {bookingTypeSlug}";
                body = $"Your booking for {bookingTypeSlug} is now {status}.";
            }

            var message = new NotificationMessage(
                Recipient: customerEmail,
                Subject: subject,
                Body: body,
                TemplateId: null,
                Metadata: new Dictionary<string, string>
                {
                    ["eventType"] = eventName,
                    ["channelType"] = channelType
                });

            await channel.SendAsync(message, ct);
            await outboxRepo.MarkDeliveredAsync(entry.Id, now, ct);

            logger.LogInformation(
                "Delivered notification {EntryId} via {ChannelType}",
                entry.Id, channelType);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Failed to deliver notification {EntryId} via {ChannelType}",
                entry.Id, channelType);
            await MarkFailureAsync(entry, now, outboxRepo, ct);
        }
    }

    private static string? ExtractChannelType(string eventType)
    {
        // "notification.booking_confirmed.email" → "email"
        var lastDot = eventType.LastIndexOf('.');
        return lastDot > 0 ? eventType[(lastDot + 1)..] : null;
    }

    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;
    }

    private static async Task MarkFailureAsync(
        PendingOutboxEntry entry,
        DateTimeOffset now,
        IWebhookOutboxRepository outboxRepo,
        CancellationToken ct)
    {
        var newAttemptCount = entry.AttemptCount + 1;
        var isFinal = newAttemptCount >= WebhookOutboxEntry.MaxAttempts;
        DateTimeOffset? nextRetryAt = isFinal
            ? null
            : now.Add(WebhookOutboxEntry.GetBackOffDelay(newAttemptCount));

        await outboxRepo.MarkFailedAttemptAsync(
            entry.Id, newAttemptCount, now, nextRetryAt, isFinal, ct);
    }
}
