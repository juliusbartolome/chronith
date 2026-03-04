using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

/// <summary>
/// Read/write access to BookingType aggregate.
/// All reads must use AsNoTracking.
/// </summary>
public interface IBookingTypeRepository
{
    Task<BookingType?> GetBySlugAsync(Guid tenantId, string slug, CancellationToken ct = default);
    Task<BookingType?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BookingType>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(BookingType bookingType, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(Guid tenantId, string slug, CancellationToken ct = default);

    Task<BookingTypeMetrics> GetTypeMetricsAsync(Guid tenantId, CancellationToken ct = default);
}
