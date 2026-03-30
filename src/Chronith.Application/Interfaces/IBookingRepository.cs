using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

/// <summary>
/// Read/write access to Booking aggregate.
/// All reads must use AsNoTracking.
/// </summary>
public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid tenantId, Guid bookingId, CancellationToken ct = default);

    Task<(IReadOnlyList<Booking> Items, int TotalCount)> ListAsync(
        Guid tenantId,
        Guid bookingTypeId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// COUNT query — runs as SQL, never in-memory.
    /// Returns the number of bookings that overlap the effective conflict range.
    /// </summary>
    Task<int> CountConflictsAsync(
        Guid bookingTypeId,
        DateTimeOffset effectiveStart,
        DateTimeOffset effectiveEnd,
        IReadOnlyList<BookingStatus> conflictStatuses,
        CancellationToken ct = default);

    /// <summary>
    /// Projects only Start+End for availability calculation. One DB round-trip.
    /// </summary>
    Task<IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)>> GetBookedSlotsAsync(
        Guid bookingTypeId,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<BookingStatus> statuses,
        CancellationToken ct = default);

    Task AddAsync(Booking booking, CancellationToken ct = default);

    Task UpdateAsync(Booking booking, CancellationToken ct = default);

    /// <summary>
    /// Public-safe update that bypasses the global tenant query filter.
    /// Uses explicit Id + TenantId filters. Safe for anonymous / cross-tenant endpoints.
    /// </summary>
    Task UpdatePublicAsync(Booking booking, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Looks up a booking by its payment reference (provider transaction ID).
    /// When tenantId is Guid.Empty, searches across all tenants (for webhook processing).
    /// </summary>
    Task<Booking?> GetByPaymentReferenceAsync(Guid tenantId, string paymentReference, CancellationToken ct = default);

    /// <summary>
    /// Public-safe lookup that bypasses the global tenant query filter.
    /// Uses explicit TenantId + IsDeleted filters. Safe for anonymous endpoints.
    /// </summary>
    Task<Booking?> GetPublicByIdAsync(Guid tenantId, Guid bookingId, CancellationToken ct = default);

    /// <summary>
    /// Returns all bookings for a specific customer (identified by CustomerId string field) within a tenant.
    /// </summary>
    Task<IReadOnlyList<Booking>> GetByCustomerIdAsync(
        Guid tenantId, string customerId, CancellationToken ct = default);

    Task<BookingMetrics> GetMetricsAsync(
        Guid tenantId, DateTimeOffset monthStartUtc, CancellationToken ct = default);

    /// <summary>
    /// Lightweight projection for iCal feed — returns only Id, Start, End for confirmed bookings.
    /// </summary>
    Task<IReadOnlyList<(Guid Id, DateTimeOffset Start, DateTimeOffset End)>> GetICalEntriesAsync(
        Guid bookingTypeId, CancellationToken ct = default);

    /// <summary>
    /// Flat projection with booking type name and staff name joined — for export only.
    /// Capped at 10,000 rows. Optional filters narrow the result set.
    /// </summary>
    Task<IReadOnlyList<BookingExportRowDto>> ListForExportAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        string? status = null,
        string? bookingTypeSlug = null,
        Guid? staffMemberId = null,
        CancellationToken ct = default);

    /// <summary>
    /// COUNT of non-cancelled bookings since a given UTC timestamp. Used by plan enforcement.
    /// </summary>
    Task<int> CountByTenantSinceAsync(Guid tenantId, DateTimeOffset since, CancellationToken ct = default);
}
