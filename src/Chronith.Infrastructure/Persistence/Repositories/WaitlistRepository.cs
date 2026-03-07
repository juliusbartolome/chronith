using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class WaitlistRepository : IWaitlistRepository
{
    private readonly ChronithDbContext _db;

    public WaitlistRepository(ChronithDbContext db) => _db = db;

    public async Task<WaitlistEntry?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _db.WaitlistEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == id, ct);

        return entity is null ? null : WaitlistEntryEntityMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<WaitlistEntry>> ListBySlotAsync(
        Guid tenantId, Guid bookingTypeId,
        DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
    {
        var entities = await _db.WaitlistEntries
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId
                        && w.BookingTypeId == bookingTypeId
                        && w.DesiredStart < end
                        && w.DesiredEnd > start)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(WaitlistEntryEntityMapper.ToDomain).ToList();
    }

    public async Task<WaitlistEntry?> GetNextWaitingAsync(
        Guid tenantId, Guid bookingTypeId,
        DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
    {
        var entity = await _db.WaitlistEntries
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId
                        && w.BookingTypeId == bookingTypeId
                        && w.Status == WaitlistStatus.Waiting
                        && w.DesiredStart < end
                        && w.DesiredEnd > start)
            .OrderBy(w => w.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : WaitlistEntryEntityMapper.ToDomain(entity);
    }

    public async Task AddAsync(WaitlistEntry entry, CancellationToken ct = default)
    {
        var entity = WaitlistEntryEntityMapper.ToEntity(entry);
        await _db.WaitlistEntries.AddAsync(entity, ct);
    }

    public async Task UpdateAsync(WaitlistEntry entry, CancellationToken ct = default)
    {
        await _db.WaitlistEntries
            .Where(w => w.Id == entry.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(w => w.Status, entry.Status)
                .SetProperty(w => w.OfferedAt, entry.OfferedAt)
                .SetProperty(w => w.ExpiresAt, entry.ExpiresAt)
                .SetProperty(w => w.IsDeleted, entry.IsDeleted),
                ct);
    }
}
