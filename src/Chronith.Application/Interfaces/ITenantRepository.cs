using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default);
    Task AddAsync(Tenant tenant, CancellationToken ct = default);
    Task<IReadOnlyList<Tenant>> ListAllAsync(CancellationToken ct = default);
}
