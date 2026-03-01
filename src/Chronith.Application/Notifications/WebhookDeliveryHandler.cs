using MediatR;
using Microsoft.Extensions.Logging;

namespace Chronith.Application.Notifications;

/// <summary>
/// Handles webhook delivery intent.
/// v0.1: Stub — logs only, no HTTP call.
/// v0.2: Will perform real HTTP delivery.
/// </summary>
public sealed class WebhookDeliveryHandler(
    ILogger<WebhookDeliveryHandler> logger)
    : INotificationHandler<BookingStatusChangedNotification>
{
    public Task Handle(BookingStatusChangedNotification notification, CancellationToken ct)
    {
        logger.LogInformation(
            "[WebhookDelivery STUB] BookingId={BookingId} transitioned {From} → {To}",
            notification.BookingId,
            notification.FromStatus?.ToString() ?? "—",
            notification.ToStatus);

        return Task.CompletedTask;
    }
}
