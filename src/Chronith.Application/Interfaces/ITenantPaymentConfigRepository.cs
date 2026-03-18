using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ITenantPaymentConfigRepository
{
    Task<TenantPaymentConfig?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenantPaymentConfig?> GetActiveByProviderNameAsync(Guid tenantId, string providerName, CancellationToken ct = default);
    Task<IReadOnlyList<TenantPaymentConfig>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantPaymentConfig>> ListActiveByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(TenantPaymentConfig config, CancellationToken ct = default);
    Task UpdateAsync(TenantPaymentConfig config, CancellationToken ct = default);
    Task DeactivateAllByProviderNameAsync(Guid tenantId, string providerName, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}
