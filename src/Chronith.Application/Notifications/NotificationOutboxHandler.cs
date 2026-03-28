using System.Text.Json;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Notifications;

public sealed class NotificationOutboxHandler(
    INotificationConfigRepository configRepo,
    IWebhookOutboxRepository outboxRepo)
    : INotificationHandler<BookingStatusChangedNotification>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task Handle(BookingStatusChangedNotification notification, CancellationToken ct)
    {
        var eventType = notification.ToStatus switch
        {
            BookingStatus.PendingVerification => "notification.payment_received",
            BookingStatus.Confirmed           => "notification.booking_confirmed",
            BookingStatus.Cancelled           => "notification.booking_cancelled",
            BookingStatus.PaymentFailed       => "notification.payment_failed",
            _                                 => null
        };

        if (eventType is null) return;

        var enabledConfigs = await configRepo.ListEnabledByTenantAsync(
            notification.TenantId, ct);

        if (enabledConfigs.Count == 0) return;

        var payload = new NotificationPayload(
            Event: eventType,
            BookingId: notification.BookingId,
            TenantId: notification.TenantId,
            BookingTypeSlug: notification.BookingTypeSlug,
            Status: notification.ToStatus.ToString(),
            Start: notification.Start,
            End: notification.End,
            CustomerId: notification.CustomerId,
            CustomerEmail: notification.CustomerEmail,
            CustomerFirstName: notification.CustomerFirstName,
            CustomerLastName: notification.CustomerLastName,
            CustomerMobile: notification.CustomerMobile,
            OccurredAt: DateTimeOffset.UtcNow);

        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

        var entries = enabledConfigs.Select(config => new WebhookOutboxEntry
        {
            TenantId = notification.TenantId,
            WebhookId = null,
            BookingTypeId = notification.BookingTypeId,
            BookingId = notification.BookingId,
            EventType = $"{eventType}.{config.ChannelType}",
            Payload = payloadJson,
            Category = OutboxCategory.Notification,
        }).ToList();

        await outboxRepo.AddRangeAsync(entries, ct);
    }
}

file sealed record NotificationPayload(
    string Event,
    Guid BookingId,
    Guid TenantId,
    string BookingTypeSlug,
    string Status,
    DateTimeOffset Start,
    DateTimeOffset End,
    string CustomerId,
    string CustomerEmail,
    string CustomerFirstName,
    string CustomerLastName,
    string? CustomerMobile,
    DateTimeOffset OccurredAt);
