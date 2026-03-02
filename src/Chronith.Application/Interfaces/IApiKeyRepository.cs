using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IApiKeyRepository
{
    Task AddAsync(TenantApiKey key, CancellationToken ct);
    Task<IReadOnlyList<TenantApiKey>> ListByTenantAsync(Guid tenantId, CancellationToken ct);
    Task<TenantApiKey?> GetByHashAsync(string keyHash, CancellationToken ct);
    Task<TenantApiKey?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct);
    Task UpdateAsync(TenantApiKey key, CancellationToken ct);
}
