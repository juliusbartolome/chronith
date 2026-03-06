using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Notifications;

/// <summary>
/// Invalidates Redis cache entries when a booking status changes.
/// Availability slots are cached with a short TTL (2 min) and expire naturally;
/// only the metrics snapshot requires immediate invalidation.
/// </summary>
public sealed class CacheInvalidationHandler(IRedisCacheService? cacheService = null)
    : INotificationHandler<BookingStatusChangedNotification>
{
    public async Task Handle(BookingStatusChangedNotification notification, CancellationToken ct)
    {
        if (cacheService is null) return;

        await cacheService.InvalidateAsync($"metrics:{notification.TenantId}", ct);
    }
}
