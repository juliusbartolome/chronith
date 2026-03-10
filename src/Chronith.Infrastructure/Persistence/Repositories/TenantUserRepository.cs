using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class TenantUserRepository(ChronithDbContext context) : ITenantUserRepository
{
    public async Task AddAsync(TenantUser user, CancellationToken ct = default)
    {
        var entity = user.ToEntity();
        await context.TenantUsers.AddAsync(entity, ct);
    }

    public async Task<TenantUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await context.TenantUsers
            .TagWith("GetByIdAsync — TenantUserRepository")
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<TenantUser?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        var normalised = email.ToLowerInvariant();
        var entity = await context.TenantUsers
            .TagWith("GetByEmailAsync — TenantUserRepository")
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == normalised, ct);
        return entity?.ToDomain();
    }

    public async Task<bool> ExistsByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        var normalised = email.ToLowerInvariant();
        return await context.TenantUsers
            .TagWith("ExistsByEmailAsync — TenantUserRepository")
            .AnyAsync(u => u.TenantId == tenantId && u.Email == normalised, ct);
    }

    public void Update(TenantUser user)
    {
        var entity = user.ToEntity();
        context.TenantUsers.Update(entity);
    }
}
