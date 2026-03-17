using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IIdempotencyKeyRepository
{
    Task<IdempotencyKey?> GetByKeyAndRouteAsync(Guid tenantId, string key, string endpointRoute, CancellationToken ct = default);
    Task AddAsync(IdempotencyKey idempotencyKey, CancellationToken ct = default);
    Task DeleteExpiredAsync(CancellationToken ct = default);
}
