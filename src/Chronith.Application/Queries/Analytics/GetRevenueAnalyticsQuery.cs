using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Queries.Analytics;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetRevenueAnalyticsQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    string GroupBy = "day")
    : IRequest<RevenueAnalyticsDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetRevenueAnalyticsHandler(
    ITenantContext tenantContext,
    IAnalyticsRepository analyticsRepo,
    IRedisCacheService? cacheService = null)
    : IRequestHandler<GetRevenueAnalyticsQuery, RevenueAnalyticsDto>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<RevenueAnalyticsDto> Handle(
        GetRevenueAnalyticsQuery query, CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId;
        var cacheKey = $"analytics:revenue:{tenantId}:{query.From:yyyyMMdd}:{query.To:yyyyMMdd}:{query.GroupBy}";

        if (cacheService is not null)
        {
            return (await cacheService.GetOrSetAsync(
                cacheKey,
                () => analyticsRepo.GetRevenueAnalyticsAsync(
                    tenantId, query.From, query.To, query.GroupBy, ct),
                CacheTtl,
                ct))!;
        }

        return await analyticsRepo.GetRevenueAnalyticsAsync(
            tenantId, query.From, query.To, query.GroupBy, ct);
    }
}
