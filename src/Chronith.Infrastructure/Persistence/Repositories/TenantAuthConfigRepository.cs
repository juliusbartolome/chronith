using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class TenantAuthConfigRepository(ChronithDbContext db) : ITenantAuthConfigRepository
{
    public async Task<TenantAuthConfig?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        var entity = await db.TenantAuthConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        return entity?.ToDomain();
    }

    public async Task AddAsync(TenantAuthConfig config, CancellationToken ct = default) =>
        await db.TenantAuthConfigs.AddAsync(config.ToEntity(), ct);

    public void Update(TenantAuthConfig config) =>
        db.TenantAuthConfigs.Update(config.ToEntity());
}
