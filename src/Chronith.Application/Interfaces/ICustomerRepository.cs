using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Customer?> GetByIdCrossTenantAsync(Guid id, CancellationToken ct = default);
    Task<Customer?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default);
    Task<Customer?> GetByExternalIdAsync(Guid tenantId, string externalId, CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
    void Update(Customer customer);
}
