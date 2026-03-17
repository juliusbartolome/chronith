using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class TimeBlockRepository : ITimeBlockRepository
{
    private readonly ChronithDbContext _db;

    public TimeBlockRepository(ChronithDbContext db) => _db = db;

    public async Task<IReadOnlyList<TimeBlock>> ListInRangeAsync(
        Guid tenantId, Guid? bookingTypeId, Guid? staffMemberId,
        DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
    {
        var query = _db.TimeBlocks
            .TagWith("ListInRangeAsync — TimeBlockRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.Start < to && t.End > from);

        if (bookingTypeId.HasValue)
            query = query.Where(t => t.BookingTypeId == null || t.BookingTypeId == bookingTypeId);

        if (staffMemberId.HasValue)
            query = query.Where(t => t.StaffMemberId == null || t.StaffMemberId == staffMemberId);

        var entities = await query
            .OrderBy(t => t.Start)
            .ToListAsync(ct);

        return entities.Select(TimeBlockEntityMapper.ToDomain).ToList();
    }

    public async Task AddAsync(TimeBlock block, CancellationToken ct = default)
    {
        var entity = TimeBlockEntityMapper.ToEntity(block);
        await _db.TimeBlocks.AddAsync(entity, ct);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        await _db.TimeBlocks
            .Where(t => t.TenantId == tenantId && t.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsDeleted, true), ct);
    }
}
