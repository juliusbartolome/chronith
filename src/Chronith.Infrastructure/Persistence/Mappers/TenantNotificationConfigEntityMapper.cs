using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class TenantNotificationConfigEntityMapper
{
    public static TenantNotificationConfig ToDomain(TenantNotificationConfigEntity entity)
    {
        var domain = new TenantNotificationConfig();
        SetProperty(domain, nameof(TenantNotificationConfig.Id), entity.Id);
        SetProperty(domain, nameof(TenantNotificationConfig.TenantId), entity.TenantId);
        SetProperty(domain, nameof(TenantNotificationConfig.ChannelType), entity.ChannelType);
        SetProperty(domain, nameof(TenantNotificationConfig.IsEnabled), entity.IsEnabled);
        SetProperty(domain, nameof(TenantNotificationConfig.Settings), entity.Settings);
        SetProperty(domain, nameof(TenantNotificationConfig.CreatedAt), entity.CreatedAt);
        SetProperty(domain, nameof(TenantNotificationConfig.UpdatedAt), entity.UpdatedAt);
        return domain;
    }

    public static TenantNotificationConfigEntity ToEntity(TenantNotificationConfig domain)
        => new()
        {
            Id = domain.Id,
            TenantId = domain.TenantId,
            ChannelType = domain.ChannelType,
            IsEnabled = domain.IsEnabled,
            Settings = domain.Settings,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt
        };

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        var prop = target.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        prop?.SetValue(target, value);
    }
}
