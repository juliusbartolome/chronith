using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class NotificationTemplateRepository(ChronithDbContext db)
    : INotificationTemplateRepository
{
    public async Task<NotificationTemplate?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await db.NotificationTemplates
            .TagWith("GetByIdAsync — NotificationTemplateRepository")
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);

        return entity?.ToDomain();
    }

    public async Task<NotificationTemplate?> GetByEventAndChannelAsync(
        Guid tenantId, string eventType, string channelType, CancellationToken ct = default)
    {
        var entity = await db.NotificationTemplates
            .TagWith("GetByEventAndChannelAsync — NotificationTemplateRepository")
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.TenantId == tenantId
                     && t.EventType == eventType
                     && t.ChannelType == channelType,
                ct);

        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<NotificationTemplate>> GetAllAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var entities = await db.NotificationTemplates
            .TagWith("GetAllAsync — NotificationTemplateRepository")
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.EventType)
            .ThenBy(t => t.ChannelType)
            .ToListAsync(ct);

        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task AddRangeAsync(
        IEnumerable<NotificationTemplate> templates, CancellationToken ct = default)
    {
        var entities = templates.Select(t => t.ToEntity()).ToList();
        await db.NotificationTemplates.AddRangeAsync(entities, ct);
    }

    public async Task UpdateAsync(
        NotificationTemplate template, CancellationToken ct = default)
    {
        var entity = await db.NotificationTemplates
            .FirstOrDefaultAsync(t => t.TenantId == template.TenantId && t.Id == template.Id, ct);

        if (entity is null) return;

        entity.Subject = template.Subject;
        entity.Body = template.Body;
        entity.IsActive = template.IsActive;
        entity.UpdatedAt = template.UpdatedAt;
    }

    public async Task DeleteByEventTypeAsync(
        Guid tenantId, string eventType, CancellationToken ct = default)
    {
        await db.NotificationTemplates
            .Where(t => t.TenantId == tenantId && t.EventType == eventType)
            .ExecuteDeleteAsync(ct);
    }
}
