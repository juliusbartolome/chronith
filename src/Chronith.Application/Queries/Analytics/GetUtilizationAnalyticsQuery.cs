using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Queries.Analytics;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetUtilizationAnalyticsQuery(
    DateTimeOffset From,
    DateTimeOffset To)
    : IRequest<UtilizationAnalyticsDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetUtilizationAnalyticsHandler(
    ITenantContext tenantContext,
    IAnalyticsRepository analyticsRepo,
    IRedisCacheService? cacheService = null)
    : IRequestHandler<GetUtilizationAnalyticsQuery, UtilizationAnalyticsDto>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<UtilizationAnalyticsDto> Handle(
        GetUtilizationAnalyticsQuery query, CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId;
        var cacheKey = $"analytics:utilization:{tenantId}:{query.From:yyyyMMdd}:{query.To:yyyyMMdd}";

        if (cacheService is not null)
        {
            return (await cacheService.GetOrSetAsync(
                cacheKey,
                () => analyticsRepo.GetUtilizationAnalyticsAsync(
                    tenantId, query.From, query.To, ct),
                CacheTtl,
                ct))!;
        }

        return await analyticsRepo.GetUtilizationAnalyticsAsync(
            tenantId, query.From, query.To, ct);
    }
}
