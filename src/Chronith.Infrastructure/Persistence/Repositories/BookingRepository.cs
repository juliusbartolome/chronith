using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository(
    ChronithDbContext _db,
    IEncryptionService encryptionService,
    ILogger<BookingRepository> logger)
    : IBookingRepository
{
    private string DecryptCustomerEmail(string? value)
    {
        if (value is null) return string.Empty;
        try { return encryptionService.Decrypt(value) ?? string.Empty; }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            logger.LogWarning("Booking.CustomerEmail could not be decrypted — " +
                "treating as legacy plaintext row. Next write will encrypt it.");
            return value;
        }
    }

    public async Task<Booking?> GetByIdAsync(Guid tenantId, Guid bookingId, CancellationToken ct = default)
    {
        var entity = await _db.Bookings
            .TagWith("GetByIdAsync — BookingRepository")
            .AsNoTracking()
            .Include(b => b.StatusChanges)
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == bookingId, ct);

        if (entity is null) return null;
        entity.CustomerEmail = DecryptCustomerEmail(entity.CustomerEmail);
        return BookingEntityMapper.ToDomain(entity);
    }

    public async Task<(IReadOnlyList<Booking> Items, int TotalCount)> ListAsync(
        Guid tenantId,
        Guid bookingTypeId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Bookings
            .TagWith("ListAsync — BookingRepository")
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.BookingTypeId == bookingTypeId)
            .OrderByDescending(b => b.Start);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items.Select(e =>
        {
            e.CustomerEmail = DecryptCustomerEmail(e.CustomerEmail);
            return BookingEntityMapper.ToDomain(e);
        }).ToList(), total);
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
            .TagWith("CountConflictsAsync — BookingRepository")
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
            .TagWith("GetBookedSlotsAsync — BookingRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(b => b.BookingTypeId == bookingTypeId
                        && !b.IsDeleted
                        && statusList.Contains(b.Status)
                        && b.Start < to
                        && b.End > from)
            .Select(b => new { b.Start, b.End })
            .ToListAsync(ct);

        return results.Select(r => (r.Start, r.End)).ToList();
    }

    public async Task<IReadOnlyList<Booking>> GetByCustomerIdAsync(
        Guid tenantId, string customerId, CancellationToken ct = default)
    {
        var entities = await _db.Bookings
            .TagWith("GetByCustomerIdAsync — BookingRepository")
            .AsNoTracking()
            .Include(b => b.StatusChanges)
            .Where(b => b.TenantId == tenantId && b.CustomerId == customerId)
            .OrderByDescending(b => b.Start)
            .ToListAsync(ct);

        return entities.Select(e =>
        {
            e.CustomerEmail = DecryptCustomerEmail(e.CustomerEmail);
            return BookingEntityMapper.ToDomain(e);
        }).ToList();
    }

    public async Task<BookingMetrics> GetMetricsAsync(
        Guid tenantId, DateTimeOffset monthStartUtc, CancellationToken ct = default)
    {
        var grouped = await _db.Bookings
            .TagWith("GetMetricsAsync.grouped — BookingRepository")
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && !b.IsDeleted)
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var total = grouped.Sum(g => g.Count);
        var byStatus = grouped.ToDictionary(g => g.Status, g => g.Count);

        var thisMonth = await _db.Bookings
            .TagWith("GetMetricsAsync.thisMonth — BookingRepository")
            .AsNoTracking()
            .CountAsync(b => b.TenantId == tenantId && !b.IsDeleted && b.Start >= monthStartUtc, ct);

        return new BookingMetrics(total, byStatus, thisMonth);
    }

    public async Task<Booking?> GetPublicByIdAsync(Guid tenantId, Guid bookingId, CancellationToken ct = default)
    {
        var entity = await _db.Bookings
            .TagWith("GetPublicByIdAsync — BookingRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(b => b.StatusChanges)
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == bookingId && !b.IsDeleted, ct);

        if (entity is null) return null;
        entity.CustomerEmail = DecryptCustomerEmail(entity.CustomerEmail);
        return BookingEntityMapper.ToDomain(entity);
    }

    public async Task<Booking?> GetByPaymentReferenceAsync(
        Guid tenantId, string paymentReference, CancellationToken ct = default)
    {
        Persistence.Entities.BookingEntity? entity = await (
            tenantId == Guid.Empty
                // Webhook context — search across all tenants, bypass query filters
                ? _db.Bookings
                    .TagWith("GetByPaymentReferenceAsync.crossTenant — BookingRepository")
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Include(b => b.StatusChanges)
                    .Where(b => !b.IsDeleted && b.PaymentReference == paymentReference)
                : _db.Bookings
                    .TagWith("GetByPaymentReferenceAsync — BookingRepository")
                    .AsNoTracking()
                    .Include(b => b.StatusChanges)
                    .Where(b => b.TenantId == tenantId && b.PaymentReference == paymentReference)
        ).FirstOrDefaultAsync(ct);

        if (entity is null) return null;
        entity.CustomerEmail = DecryptCustomerEmail(entity.CustomerEmail);
        return BookingEntityMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<(Guid Id, DateTimeOffset Start, DateTimeOffset End)>> GetICalEntriesAsync(
        Guid bookingTypeId, CancellationToken ct = default)
    {
        var results = await _db.Bookings
            .TagWith("GetICalEntriesAsync — BookingRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(b => b.BookingTypeId == bookingTypeId
                        && !b.IsDeleted
                        && b.Status == BookingStatus.Confirmed)
            .Select(b => new { b.Id, b.Start, b.End })
            .ToListAsync(ct);

        return results.Select(r => (r.Id, r.Start, r.End)).ToList();
    }

    public async Task AddAsync(Booking booking, CancellationToken ct = default)
    {
        var entity = BookingEntityMapper.ToEntity(booking);
        entity.CustomerEmail = encryptionService.Encrypt(entity.CustomerEmail) ?? string.Empty;
        await _db.Bookings.AddAsync(entity, ct);
    }

    public async Task<IReadOnlyList<BookingExportRowDto>> ListForExportAsync(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        string? status = null,
        string? bookingTypeSlug = null,
        Guid? staffMemberId = null,
        CancellationToken ct = default)
    {
        var rawItems = await _db.Bookings
            .TagWith("ListForExportAsync — BookingRepository")
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.Start >= from && b.Start <= to)
            .Where(b => status == null || b.Status.ToString() == status)
            .Where(b => bookingTypeSlug == null || b.BookingType!.Slug == bookingTypeSlug)
            .Where(b => staffMemberId == null || b.StaffMemberId == staffMemberId)
            .OrderBy(b => b.Start)
            .Take(10_000)
            .Include(b => b.BookingType)
            .Include(b => b.StaffMember)
            .ToListAsync(ct);

        return rawItems.Select(b => new BookingExportRowDto(
            b.Id,
            b.BookingType != null ? b.BookingType.Name : string.Empty,
            b.BookingType != null ? b.BookingType.Slug : string.Empty,
            b.Start,
            b.End,
            b.Status.ToString(),
            DecryptCustomerEmail(b.CustomerEmail),
            b.CustomerId,
            b.StaffMember != null ? b.StaffMember.Name : null,
            b.AmountInCentavos,
            b.Currency,
            b.PaymentReference)).ToList();
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

        await InsertNewStatusChangesAsync(booking, ct);
    }

    public async Task UpdatePublicAsync(Booking booking, Guid tenantId, CancellationToken ct = default)
    {
        // Bypasses global tenant query filter — mirrors GetPublicByIdAsync.
        // Uses explicit TenantId + Id + !IsDeleted filters for safety.
        await _db.Bookings
            .IgnoreQueryFilters()
            .Where(b => b.Id == booking.Id && b.TenantId == tenantId && !b.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, booking.Status)
                .SetProperty(b => b.IsDeleted, booking.IsDeleted)
                .SetProperty(b => b.PaymentReference, booking.PaymentReference)
                .SetProperty(b => b.CheckoutUrl, booking.CheckoutUrl)
                .SetProperty(b => b.ProofOfPaymentUrl, booking.ProofOfPaymentUrl)
                .SetProperty(b => b.ProofOfPaymentFileName, booking.ProofOfPaymentFileName)
                .SetProperty(b => b.PaymentNote, booking.PaymentNote),
                ct);

        await InsertNewStatusChangesAsync(booking, ct);
    }

    private async Task InsertNewStatusChangesAsync(Booking booking, CancellationToken ct)
    {
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

    public Task<int> CountByTenantSinceAsync(
        Guid tenantId, DateTimeOffset since, CancellationToken ct = default) =>
        _db.Bookings
            .TagWith("CountByTenantSinceAsync — BookingRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(b =>
                b.TenantId == tenantId &&
                !b.IsDeleted &&
                b.Status != BookingStatus.Cancelled &&
                b.Start >= since,
                ct);
}
