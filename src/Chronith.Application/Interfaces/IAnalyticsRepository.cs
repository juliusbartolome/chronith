using Chronith.Application.DTOs;

namespace Chronith.Application.Interfaces;

/// <summary>
/// Read-only aggregate queries for analytics dashboards.
/// All methods run GROUP BY queries with AsNoTracking.
/// </summary>
public interface IAnalyticsRepository
{
    Task<BookingAnalyticsDto> GetBookingAnalyticsAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        string groupBy,
        CancellationToken ct = default);

    Task<RevenueAnalyticsDto> GetRevenueAnalyticsAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        string groupBy,
        CancellationToken ct = default);

    Task<UtilizationAnalyticsDto> GetUtilizationAnalyticsAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}
