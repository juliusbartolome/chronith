using System.Text.Json;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using MediatR;

namespace Chronith.Application.Notifications;

public sealed class WebhookOutboxHandler(
    IWebhookRepository webhookRepo,
    IWebhookOutboxRepository outboxRepo,
    IUnitOfWork unitOfWork)
    : INotificationHandler<BookingStatusChangedNotification>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task Handle(BookingStatusChangedNotification notification, CancellationToken ct)
    {
        var eventType = notification.ToStatus switch
        {
            BookingStatus.PendingVerification => "booking.payment_received",
            BookingStatus.Confirmed           => "booking.confirmed",
            BookingStatus.Cancelled           => "booking.cancelled",
            _                                 => null
        };

        if (eventType is null) return;

        var webhooks = await webhookRepo.ListAsync(notification.TenantId, notification.BookingTypeId, ct);
        if (webhooks.Count == 0) return;

        var payload = new BookingEventPayload(
            Event: eventType,
            BookingId: notification.BookingId,
            TenantId: notification.TenantId,
            BookingTypeSlug: notification.BookingTypeSlug,
            Status: notification.ToStatus.ToString(),
            Start: notification.Start,
            End: notification.End,
            CustomerId: notification.CustomerId,
            CustomerEmail: notification.CustomerEmail,
            OccurredAt: DateTimeOffset.UtcNow);

        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

        var entries = webhooks.Select(w => new Domain.Models.WebhookOutboxEntry
        {
            TenantId = notification.TenantId,
            WebhookId = w.Id,
            BookingId = notification.BookingId,
            EventType = eventType,
            Payload = payloadJson,
        }).ToList();

        await outboxRepo.AddRangeAsync(entries, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
