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

    public async Task AddAsync(Booking booking, CancellationToken ct = default)
    {
        var entity = BookingEntityMapper.ToEntity(booking);
        await _db.Bookings.AddAsync(entity, ct);
    }

    public async Task UpdateAsync(Booking booking, CancellationToken ct = default)
    {
        var entity = await _db.Bookings
            .Include(b => b.StatusChanges)
            .FirstOrDefaultAsync(b => b.Id == booking.Id, ct);

        if (entity is null) return;

        entity.Status = booking.Status;
        entity.IsDeleted = booking.IsDeleted;
        entity.PaymentReference = booking.PaymentReference;

        // Sync new status changes
        var existingIds = entity.StatusChanges.Select(sc => sc.Id).ToHashSet();
        foreach (var sc in booking.StatusChanges.Where(sc => !existingIds.Contains(sc.Id)))
        {
            entity.StatusChanges.Add(new Persistence.Entities.BookingStatusChangeEntity
            {
                Id = sc.Id,
                BookingId = sc.BookingId,
                FromStatus = sc.FromStatus,
                ToStatus = sc.ToStatus,
                ChangedById = sc.ChangedById,
                ChangedByRole = sc.ChangedByRole,
                ChangedAt = sc.ChangedAt
            });
        }
    }
}
