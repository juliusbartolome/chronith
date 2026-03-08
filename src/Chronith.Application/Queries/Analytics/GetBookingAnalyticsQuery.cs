using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Queries.Analytics;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetBookingAnalyticsQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    string GroupBy = "day")
    : IRequest<BookingAnalyticsDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetBookingAnalyticsHandler(
    ITenantContext tenantContext,
    IAnalyticsRepository analyticsRepo,
    IRedisCacheService? cacheService = null)
    : IRequestHandler<GetBookingAnalyticsQuery, BookingAnalyticsDto>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<BookingAnalyticsDto> Handle(
        GetBookingAnalyticsQuery query, CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId;
        var cacheKey = $"analytics:bookings:{tenantId}:{query.From:yyyyMMdd}:{query.To:yyyyMMdd}:{query.GroupBy}";

        if (cacheService is not null)
        {
            return (await cacheService.GetOrSetAsync(
                cacheKey,
                () => analyticsRepo.GetBookingAnalyticsAsync(
                    tenantId, query.From, query.To, query.GroupBy, ct),
                CacheTtl,
                ct))!;
        }

        return await analyticsRepo.GetBookingAnalyticsAsync(
            tenantId, query.From, query.To, query.GroupBy, ct);
    }
}
