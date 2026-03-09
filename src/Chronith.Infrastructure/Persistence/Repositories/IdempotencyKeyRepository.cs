using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class IdempotencyKeyRepository(ChronithDbContext db) : IIdempotencyKeyRepository
{
    public async Task<IdempotencyKey?> GetByKeyAndRouteAsync(
        Guid tenantId, string key, string endpointRoute, CancellationToken ct = default)
    {
        var entity = await db.IdempotencyKeys.AsNoTracking()
            .FirstOrDefaultAsync(k =>
                k.TenantId == tenantId &&
                k.Key == key &&
                k.EndpointRoute == endpointRoute &&
                k.ExpiresAt > DateTimeOffset.UtcNow, ct);
        return entity?.ToDomain();
    }

    public async Task AddAsync(IdempotencyKey idempotencyKey, CancellationToken ct = default) =>
        await db.IdempotencyKeys.AddAsync(idempotencyKey.ToEntity(), ct);

    public async Task DeleteExpiredAsync(CancellationToken ct = default) =>
        await db.IdempotencyKeys
            .Where(k => k.ExpiresAt <= DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(ct);
}
