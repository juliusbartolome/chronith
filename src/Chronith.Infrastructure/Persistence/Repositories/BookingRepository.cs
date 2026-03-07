using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly ChronithDbContext _db;

    public BookingRepository(ChronithDbContext db) => _db = db;

    public async Task<Booking?> GetByIdAsync(Guid tenantId, Guid bookingId, CancellationToken ct = default)
    {
        var entity = await _db.Bookings
            .AsNoTracking()
            .Include(b => b.StatusChanges)
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == bookingId, ct);

        return entity is null ? null : BookingEntityMapper.ToDomain(entity);
    }

    public async Task<(IReadOnlyList<Booking> Items, int TotalCount)> ListAsync(
        Guid tenantId,
        Guid bookingTypeId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Bookings
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.BookingTypeId == bookingTypeId)
            .OrderByDescending(b => b.Start);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items.Select(BookingEntityMapper.ToDomain).ToList(), total);
    }

    /// <summary>
    /// COUNT query — runs as SQL, never in-memory.
    /// Conflict: effectiveStart &lt; newEnd AND effectiveEnd &gt; newStart
    /// </summary>
    public async Task<int> CountConflictsAsync(
        Guid bookingTypeId,
        DateTimeOffset effectiveStart,
        DateTimeOffset effectiveEnd,
        IReadOnlyList<BookingStatus> conflictStatuses,
        CancellationToken ct = default)
    {
        var statuses = conflictStatuses.ToList();
        return await _db.Bookings
            .AsNoTracking()
            .IgnoreQueryFilters()   // need to query across tenants to detect cross-tenant conflicts? No — but we need to bypass soft-delete filter
            .Where(b => b.BookingTypeId == bookingTypeId
                        && !b.IsDeleted
                        && statuses.Contains(b.Status)
                        && b.Start < effectiveEnd
                        && b.End > effectiveStart)
            .CountAsync(ct);
    }

    /// <summary>
    /// Projects only Start+End for availability calculation. One DB round-trip.
    /// </summary>
    public async Task<IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)>> GetBookedSlotsAsync(
        Guid bookingTypeId,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<BookingStatus> statuses,
        CancellationToken ct = default)
    {
        var statusList = statuses.ToList();
        var results = await _db.Bookings
            .AsNoTracking()
            .Where(b => b.BookingTypeId == bookingTypeId
                        && !b.IsDeleted
                        && statusList.Contains(b.Status)
                        && b.Start < to
                        && b.End > from)
            .Select(b => new { b.Start, b.End })
            .ToListAsync(ct);

        return results.Select(r => (r.Start, r.End)).ToList();
    }

    public async Task<BookingMetrics> GetMetricsAsync(
        Guid tenantId, DateTimeOffset monthStartUtc, CancellationToken ct = default)
    {
        var grouped = await _db.Bookings
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && !b.IsDeleted)
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var total = grouped.Sum(g => g.Count);
        var byStatus = grouped.ToDictionary(g => g.Status, g => g.Count);

        var thisMonth = await _db.Bookings
            .AsNoTracking()
            .CountAsync(b => b.TenantId == tenantId && !b.IsDeleted && b.Start >= monthStartUtc, ct);

        return new BookingMetrics(total, byStatus, thisMonth);
    }

    public async Task<Booking?> GetByPaymentReferenceAsync(
        Guid tenantId, string paymentReference, CancellationToken ct = default)
    {
        Persistence.Entities.BookingEntity? entity = await (
            tenantId == Guid.Empty
                // Webhook context — search across all tenants, bypass query filters
                ? _db.Bookings
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Include(b => b.StatusChanges)
                    .Where(b => !b.IsDeleted && b.PaymentReference == paymentReference)
                : _db.Bookings
                    .AsNoTracking()
                    .Include(b => b.StatusChanges)
                    .Where(b => b.TenantId == tenantId && b.PaymentReference == paymentReference)
        ).FirstOrDefaultAsync(ct);

        return entity is null ? null : BookingEntityMapper.ToDomain(entity);
    }

    public async Task AddAsync(Booking booking, CancellationToken ct = default)
    {
        var entity = BookingEntityMapper.ToEntity(booking);
        await _db.Bookings.AddAsync(entity, ct);
    }

    public async Task UpdateAsync(Booking booking, CancellationToken ct = default)
    {
        // Update scalar fields directly — avoids xmin concurrency token mismatch
        // since the booking was loaded with AsNoTracking and RowVersion is not propagated.
        await _db.Bookings
            .Where(b => b.Id == booking.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, booking.Status)
                .SetProperty(b => b.IsDeleted, booking.IsDeleted)
                .SetProperty(b => b.PaymentReference, booking.PaymentReference)
                .SetProperty(b => b.CheckoutUrl, booking.CheckoutUrl),
                ct);

        // Insert any new status change records
        var existingIds = await _db.BookingStatusChanges
            .Where(sc => sc.BookingId == booking.Id)
            .Select(sc => sc.Id)
            .ToListAsync(ct);

        var newChanges = booking.StatusChanges
            .Where(sc => !existingIds.Contains(sc.Id))
            .Select(sc => new Persistence.Entities.BookingStatusChangeEntity
            {
                Id = sc.Id,
                BookingId = sc.BookingId,
                FromStatus = sc.FromStatus,
                ToStatus = sc.ToStatus,
                ChangedById = sc.ChangedById,
                ChangedByRole = sc.ChangedByRole,
                ChangedAt = sc.ChangedAt
            })
            .ToList();

        if (newChanges.Count > 0)
            await _db.BookingStatusChanges.AddRangeAsync(newChanges, ct);
    }
}
