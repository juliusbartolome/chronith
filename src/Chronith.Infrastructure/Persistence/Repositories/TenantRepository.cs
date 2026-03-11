using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository : ITenantRepository
{
    private readonly ChronithDbContext _db;

    public TenantRepository(ChronithDbContext db) => _db = db;

    public async Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        var entity = await _db.Tenants
            .TagWith("GetByIdAsync — TenantRepository")
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        return entity is null ? null : TenantEntityMapper.ToDomain(entity);
    }

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var entity = await _db.Tenants
            .TagWith("GetBySlugAsync — TenantRepository")
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug, ct);

        return entity is null ? null : TenantEntityMapper.ToDomain(entity);
    }

    public async Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default)
        => await _db.Tenants
            .TagWith("ExistsBySlugAsync — TenantRepository")
            .AnyAsync(t => t.Slug == slug, ct);

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default)
    {
        var entity = TenantEntityMapper.ToEntity(tenant);
        await _db.Tenants.AddAsync(entity, ct);
    }
}
