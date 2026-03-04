using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ITenantUserRepository
{
    Task AddAsync(TenantUser user, CancellationToken ct = default);
    Task<TenantUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenantUser?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(Guid tenantId, string email, CancellationToken ct = default);
    void Update(TenantUser user);
}
