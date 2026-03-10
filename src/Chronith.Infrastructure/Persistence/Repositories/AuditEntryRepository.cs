using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class AuditEntryRepository : IAuditEntryRepository
{
    private readonly ChronithDbContext _db;

    public AuditEntryRepository(ChronithDbContext db) => _db = db;

    public async Task AddAsync(AuditEntry entry, CancellationToken ct)
    {
        var entity = AuditEntryEntityMapper.ToEntity(entry);
        await _db.AuditEntries.AddAsync(entity, ct);
    }

    public async Task<AuditEntry?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var entity = await _db.AuditEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);

        return entity is null ? null : AuditEntryEntityMapper.ToDomain(entity);
    }

    public async Task<(IReadOnlyList<AuditEntry> Items, int TotalCount)> QueryAsync(
        Guid tenantId,
        string? entityType,
        Guid? entityId,
        string? userId,
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var query = _db.AuditEntries
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId);

        if (entityType is not null)
            query = query.Where(a => a.EntityType == entityType);

        if (entityId is not null)
            query = query.Where(a => a.EntityId == entityId.Value);

        if (userId is not null)
            query = query.Where(a => a.UserId == userId);

        if (action is not null)
            query = query.Where(a => a.Action == action);

        if (from is not null)
            query = query.Where(a => a.Timestamp >= from.Value);

        if (to is not null)
            query = query.Where(a => a.Timestamp <= to.Value);

        query = query.OrderByDescending(a => a.Timestamp);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items.Select(AuditEntryEntityMapper.ToDomain).ToList(), total);
    }

    public async Task<int> DeleteExpiredAsync(Guid tenantId, DateTimeOffset before, CancellationToken ct)
    {
        return await _db.AuditEntries
            .Where(a => a.TenantId == tenantId && a.Timestamp < before)
            .ExecuteDeleteAsync(ct);
    }
}
