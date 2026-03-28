using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IWebhookRepository
{
    Task<IReadOnlyList<Webhook>> ListAsync(Guid tenantId, Guid bookingTypeId, CancellationToken ct = default);
    Task<Webhook?> GetByIdAsync(Guid tenantId, Guid webhookId, CancellationToken ct = default);
    Task<Webhook?> GetByIdCrossTenantAsync(Guid webhookId, CancellationToken ct = default);
    Task AddAsync(Webhook webhook, CancellationToken ct = default);
    Task UpdateAsync(Webhook webhook, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid webhookId, CancellationToken ct = default);
}
