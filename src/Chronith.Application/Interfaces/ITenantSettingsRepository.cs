using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ITenantSettingsRepository
{
    Task<TenantSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantSettings> GetOrCreateAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(TenantSettings settings, CancellationToken ct = default);
    Task UpdateAsync(TenantSettings settings, CancellationToken ct = default);
}
