using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Customer?> GetByIdCrossTenantAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Cross-tenant lookup — bypasses tenant query filter.
    /// Used by background services. Returns null if not found or soft-deleted.
    /// </summary>
    Task<Customer?> GetByIdAcrossTenantsAsync(Guid customerId, CancellationToken ct = default);
    Task<Customer?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default);
    Task<Customer?> GetByExternalIdAsync(Guid tenantId, string externalId, CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
    void Update(Customer customer);

    /// <summary>COUNT of non-deleted customers for a tenant. Used by plan enforcement.</summary>
    Task<int> CountByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
