using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class TenantPlanRepository : ITenantPlanRepository
{
    private readonly ChronithDbContext _db;

    public TenantPlanRepository(ChronithDbContext db) => _db = db;

    public async Task<IReadOnlyList<TenantPlan>> GetActivePlansAsync(CancellationToken ct = default)
    {
        var entities = await _db.TenantPlans
            .TagWith("GetActivePlansAsync — TenantPlanRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

        return entities.Select(TenantPlanEntityMapper.ToDomain).ToList();
    }

    public async Task<TenantPlan?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.TenantPlans
            .TagWith("GetByIdAsync — TenantPlanRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

        return entity is null ? null : TenantPlanEntityMapper.ToDomain(entity);
    }
}
