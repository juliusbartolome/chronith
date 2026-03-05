using System.Text.Json;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Notifications;

public sealed class WebhookOutboxHandler(
    IWebhookRepository webhookRepo,
    IWebhookOutboxRepository outboxRepo,
    IBookingTypeRepository bookingTypeRepo)
    : INotificationHandler<BookingStatusChangedNotification>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task Handle(BookingStatusChangedNotification notification, CancellationToken ct)
    {
        var tenantEventType = notification.ToStatus switch
        {
            BookingStatus.PendingVerification => "booking.payment_received",
            BookingStatus.Confirmed           => "booking.confirmed",
            BookingStatus.Cancelled           => "booking.cancelled",
            _                                 => null
        };

        var customerEventType = notification.ToStatus switch
        {
            BookingStatus.PendingVerification => "customer.payment.received",
            BookingStatus.Confirmed           => "customer.booking.confirmed",
            BookingStatus.Cancelled           => "customer.booking.cancelled",
            _                                 => null
        };

        // If neither category is triggered, do nothing
        if (tenantEventType is null && customerEventType is null) return;

        var entries = new List<WebhookOutboxEntry>();

        // ── Tenant Webhook entries ────────────────────────────────────────────
        if (tenantEventType is not null)
        {
            var webhooks = await webhookRepo.ListAsync(notification.TenantId, notification.BookingTypeId, ct);
            if (webhooks.Count > 0)
            {
                var tenantPayload = new BookingEventPayload(
                    Event: tenantEventType,
                    BookingId: notification.BookingId,
                    TenantId: notification.TenantId,
                    BookingTypeSlug: notification.BookingTypeSlug,
                    Status: notification.ToStatus.ToString(),
                    Start: notification.Start,
                    End: notification.End,
                    CustomerId: notification.CustomerId,
                    CustomerEmail: notification.CustomerEmail,
                    OccurredAt: DateTimeOffset.UtcNow);

                var tenantPayloadJson = JsonSerializer.Serialize(tenantPayload, JsonOptions);

                entries.AddRange(webhooks.Select(w => new WebhookOutboxEntry
                {
                    TenantId = notification.TenantId,
                    WebhookId = w.Id,
                    BookingTypeId = null,
                    BookingId = notification.BookingId,
                    EventType = tenantEventType,
                    Payload = tenantPayloadJson,
                    Category = OutboxCategory.TenantWebhook,
                }));
            }
        }

        // ── Customer Callback entries ─────────────────────────────────────────
        if (customerEventType is not null)
        {
            var bookingType = await bookingTypeRepo.GetByIdAsync(notification.BookingTypeId, ct);
            if (bookingType?.CustomerCallbackUrl is not null)
            {
                var callbackPayload = new CustomerCallbackPayload(
                    BookingId: notification.BookingId,
                    BookingReference: null,
                    CustomerEmail: notification.CustomerEmail,
                    EventType: customerEventType,
                    OccurredAt: DateTimeOffset.UtcNow);

                var callbackPayloadJson = JsonSerializer.Serialize(callbackPayload, JsonOptions);

                entries.Add(new WebhookOutboxEntry
                {
                    TenantId = notification.TenantId,
                    WebhookId = null,
                    BookingTypeId = notification.BookingTypeId,
                    BookingId = notification.BookingId,
                    EventType = customerEventType,
                    Payload = callbackPayloadJson,
                    Category = OutboxCategory.CustomerCallback,
                });
            }
        }

        if (entries.Count > 0)
            await outboxRepo.AddRangeAsync(entries, ct);
    }
}

// ── Payload records ────────────────────────────────────────────────────────────

file sealed record BookingEventPayload(
    string Event,
    Guid BookingId,
    Guid TenantId,
    string BookingTypeSlug,
    string Status,
    DateTimeOffset Start,
    DateTimeOffset End,
    string CustomerId,
    string CustomerEmail,
    DateTimeOffset OccurredAt);

file sealed record CustomerCallbackPayload(
    Guid BookingId,
    string? BookingReference,
    string CustomerEmail,
    string EventType,
    DateTimeOffset OccurredAt);
