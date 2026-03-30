using System.Text.Json;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using MediatR;
using Microsoft.Extensions.Options;

namespace Chronith.Application.Notifications;

public sealed class NotificationOutboxHandler(
    INotificationConfigRepository configRepo,
    IWebhookOutboxRepository outboxRepo,
    IBookingUrlSigner signer,
    ITenantRepository tenantRepo,
    IOptions<PaymentPageOptions> pageOptions)
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

        // ── Generate staff verify URL for PendingVerification transitions ─────
        string? staffVerifyUrl = null;
        if (notification.ToStatus == BookingStatus.PendingVerification)
        {
            var tenant = await tenantRepo.GetByIdAsync(notification.TenantId, ct);
            if (tenant is not null)
            {
                staffVerifyUrl = signer.GenerateStaffVerifyUrl(
                    pageOptions.Value.StaffVerifyBaseUrl,
                    notification.BookingId,
                    tenant.Slug);
            }
        }

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
            OccurredAt: DateTimeOffset.UtcNow,
            StaffVerifyUrl: staffVerifyUrl);

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
    DateTimeOffset OccurredAt,
    string? StaffVerifyUrl);
