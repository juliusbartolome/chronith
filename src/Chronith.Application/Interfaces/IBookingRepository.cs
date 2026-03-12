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
    /// Looks up a booking by its payment reference (provider transaction ID).
    /// When tenantId is Guid.Empty, searches across all tenants (for webhook processing).
    /// </summary>
    Task<Booking?> GetByPaymentReferenceAsync(Guid tenantId, string paymentReference, CancellationToken ct = default);

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
    /// </summary>
    Task<IReadOnlyList<BookingExportRowDto>> ListForExportAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}
