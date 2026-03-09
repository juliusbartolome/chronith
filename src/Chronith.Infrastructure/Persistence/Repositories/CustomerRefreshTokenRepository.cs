using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class CustomerRefreshTokenRepository(ChronithDbContext db) : ICustomerRefreshTokenRepository
{
    public async Task AddAsync(CustomerRefreshToken token, CancellationToken ct = default) =>
        await db.CustomerRefreshTokens.AddAsync(token.ToEntity(), ct);

    public async Task<CustomerRefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var entity = await db.CustomerRefreshTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
        return entity?.ToDomain();
    }

    public void Update(CustomerRefreshToken token) =>
        db.CustomerRefreshTokens.Update(token.ToEntity());
}
