using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface INotificationConfigRepository
{
    Task<TenantNotificationConfig?> GetByChannelTypeAsync(
        Guid tenantId, string channelType, CancellationToken ct = default);

    Task<IReadOnlyList<TenantNotificationConfig>> ListByTenantAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>Lists all enabled configs for a tenant. Used by outbox handler.</summary>
    Task<IReadOnlyList<TenantNotificationConfig>> ListEnabledByTenantAsync(
        Guid tenantId, CancellationToken ct = default);

    Task AddAsync(TenantNotificationConfig config, CancellationToken ct = default);

    Task UpdateAsync(TenantNotificationConfig config, CancellationToken ct = default);
}
