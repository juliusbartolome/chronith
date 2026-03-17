using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class TenantPlanRepository(ChronithDbContext db) : ITenantPlanRepository
{
    public async Task<IReadOnlyList<TenantPlan>> GetActivePlansAsync(CancellationToken ct = default)
    {
        var entities = await db.TenantPlans
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
        var entity = await db.TenantPlans
            .TagWith("GetByIdAsync — TenantPlanRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

        return entity is null ? null : TenantPlanEntityMapper.ToDomain(entity);
    }
}
