using System.Security.Cryptography;
using System.Text;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Services;

public sealed class WebhookDispatcherService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IOptions<WebhookDispatcherOptions> options,
    IBackgroundServiceHealthTracker healthTracker,
    ILogger<WebhookDispatcherService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
                healthTracker.RecordSuccess(nameof(WebhookDispatcherService));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during webhook dispatch batch");
            }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.DispatchIntervalSeconds), stoppingToken);
        }
    }

    // internal to allow unit tests via InternalsVisibleTo
    internal async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IWebhookOutboxRepository>();
        var webhookRepo = scope.ServiceProvider.GetRequiredService<IWebhookRepository>();
        var bookingTypeRepo = scope.ServiceProvider.GetRequiredService<IBookingTypeRepository>();

        var pending = await outboxRepo.GetPendingAsync(50, ct);
        if (pending.Count == 0) return;

        foreach (var entry in pending)
        {
            if (entry.Category == OutboxCategory.CustomerCallback)
            {
                await DispatchCustomerCallbackAsync(entry, bookingTypeRepo, outboxRepo, ct);
            }
            else
            {
                // TenantWebhook — existing behaviour
                var webhook = await webhookRepo.GetByIdCrossTenantAsync(entry.WebhookId!.Value, ct);
                if (webhook is null)
                {
                    logger.LogWarning(
                        "Webhook {WebhookId} not found for outbox entry {EntryId} — marking as permanently failed",
                        entry.WebhookId, entry.Id);
                    await outboxRepo.MarkFailedAttemptAsync(
                        entry.Id, WebhookOutboxEntry.MaxAttempts, DateTimeOffset.UtcNow, null, true, ct);
                    continue;
                }

                await DispatchAsync(entry, webhook.Url, webhook.Secret, outboxRepo, ct);
            }
        }
    }

    private async Task DispatchCustomerCallbackAsync(
        PendingOutboxEntry entry,
        IBookingTypeRepository bookingTypeRepo,
        IWebhookOutboxRepository outboxRepo,
        CancellationToken ct)
    {
        var bookingType = entry.BookingTypeId.HasValue
            ? await bookingTypeRepo.GetByIdAsync(entry.BookingTypeId.Value, ct)
            : null;

        if (bookingType?.CustomerCallbackUrl is null)
        {
            logger.LogWarning(
                "CustomerCallback URL not found for BookingType {BookingTypeId} on entry {EntryId} — marking as abandoned",
                entry.BookingTypeId, entry.Id);
            await outboxRepo.MarkAbandonedAsync(entry.Id, ct);
            return;
        }

        var secret = bookingType.CustomerCallbackSecret ?? string.Empty;
        await DispatchAsync(entry, bookingType.CustomerCallbackUrl, secret, outboxRepo, ct);
    }

    private async Task DispatchAsync(
        PendingOutboxEntry entry,
        string url,
        string secret,
        IWebhookOutboxRepository outboxRepo,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var httpClient = httpClientFactory.CreateClient("WebhookDispatcher");

        try
        {
            var signature = ComputeHmacSignature(entry.Payload, secret);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(entry.Payload, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("X-Chronith-Event", entry.EventType);
            request.Headers.TryAddWithoutValidation("X-Chronith-Delivery", entry.Id.ToString());
            request.Headers.TryAddWithoutValidation("X-Chronith-Signature", $"sha256={signature}");

            var response = await httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                await outboxRepo.MarkDeliveredAsync(entry.Id, now, ct);
                logger.LogInformation("Delivered outbox entry {EntryId} to {Url}", entry.Id, url);
            }
            else
            {
                logger.LogWarning("Non-success {Status} from {Url} for entry {EntryId}",
                    (int)response.StatusCode, url, entry.Id);
                await MarkFailureAsync(entry, now, outboxRepo, ct);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "HTTP error delivering outbox entry {EntryId} to {Url}", entry.Id, url);
            await MarkFailureAsync(entry, now, outboxRepo, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // Timeout — not a graceful shutdown; treat as delivery failure
            logger.LogWarning(ex, "HTTP timeout delivering outbox entry {EntryId} to {Url}", entry.Id, url);
            await MarkFailureAsync(entry, now, outboxRepo, ct);
        }
        // OperationCanceledException (shutdown) propagates naturally
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

        await outboxRepo.MarkFailedAttemptAsync(entry.Id, newAttemptCount, now, nextRetryAt, isFinal, ct);
    }

    private static string ComputeHmacSignature(string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var message = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, message);
        return Convert.ToHexStringLower(hash);
    }
}
