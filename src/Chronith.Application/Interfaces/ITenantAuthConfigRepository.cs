using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ITenantAuthConfigRepository
{
    Task<TenantAuthConfig?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(TenantAuthConfig config, CancellationToken ct = default);
    void Update(TenantAuthConfig config);
}
