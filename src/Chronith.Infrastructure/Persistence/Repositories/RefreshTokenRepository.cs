using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(ChronithDbContext context) : IRefreshTokenRepository
{
    public async Task AddAsync(TenantUserRefreshToken token, CancellationToken ct = default)
    {
        var entity = token.ToEntity();
        await context.TenantUserRefreshTokens.AddAsync(entity, ct);
    }

    public async Task<TenantUserRefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var entity = await context.TenantUserRefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
        return entity?.ToDomain();
    }

    public void Update(TenantUserRefreshToken token)
    {
        var entity = token.ToEntity();
        context.TenantUserRefreshTokens.Update(entity);
    }
}
