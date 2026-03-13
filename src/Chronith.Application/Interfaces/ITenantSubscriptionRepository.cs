using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ITenantSubscriptionRepository
{
    Task<TenantSubscription?> GetActiveByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantSubscription?> GetByProviderIdAsync(string providerSubscriptionId, CancellationToken ct = default);
    Task AddAsync(TenantSubscription subscription, CancellationToken ct = default);
    Task UpdateAsync(TenantSubscription subscription, CancellationToken ct = default);
}
