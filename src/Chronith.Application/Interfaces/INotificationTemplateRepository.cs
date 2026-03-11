using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface INotificationTemplateRepository
{
    Task<NotificationTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<NotificationTemplate?> GetByEventAndChannelAsync(Guid tenantId, string eventType, string channelType, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationTemplate>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<NotificationTemplate> templates, CancellationToken ct = default);
    Task UpdateAsync(NotificationTemplate template, CancellationToken ct = default);
    Task DeleteByEventTypeAsync(Guid tenantId, string eventType, CancellationToken ct = default);
}
