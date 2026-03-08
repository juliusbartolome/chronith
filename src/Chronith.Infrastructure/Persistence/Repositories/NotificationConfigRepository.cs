using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class NotificationConfigRepository(ChronithDbContext db)
    : INotificationConfigRepository
{
    public async Task<TenantNotificationConfig?> GetByChannelTypeAsync(
        Guid tenantId, string channelType, CancellationToken ct = default)
    {
        var entity = await db.TenantNotificationConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ChannelType == channelType, ct);

        return entity is null ? null : TenantNotificationConfigEntityMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<TenantNotificationConfig>> ListByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var entities = await db.TenantNotificationConfigs
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.ChannelType)
            .ToListAsync(ct);

        return entities.Select(TenantNotificationConfigEntityMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<TenantNotificationConfig>> ListEnabledByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var entities = await db.TenantNotificationConfigs
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsEnabled)
            .ToListAsync(ct);

        return entities.Select(TenantNotificationConfigEntityMapper.ToDomain).ToList();
    }

    public async Task AddAsync(TenantNotificationConfig config, CancellationToken ct = default)
    {
        var entity = TenantNotificationConfigEntityMapper.ToEntity(config);
        await db.TenantNotificationConfigs.AddAsync(entity, ct);
    }

    public async Task UpdateAsync(TenantNotificationConfig config, CancellationToken ct = default)
    {
        await db.TenantNotificationConfigs
            .Where(c => c.Id == config.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsEnabled, config.IsEnabled)
                .SetProperty(c => c.Settings, config.Settings)
                .SetProperty(c => c.UpdatedAt, config.UpdatedAt),
                ct);
    }
}
