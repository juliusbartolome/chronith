using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class NotificationTemplateEntityMapper
{
    public static NotificationTemplate ToDomain(this NotificationTemplateEntity entity)
    {
        var domain = new NotificationTemplate();
        SetProperty(domain, nameof(NotificationTemplate.Id), entity.Id);
        SetProperty(domain, nameof(NotificationTemplate.TenantId), entity.TenantId);
        SetProperty(domain, nameof(NotificationTemplate.EventType), entity.EventType);
        SetProperty(domain, nameof(NotificationTemplate.ChannelType), entity.ChannelType);
        SetProperty(domain, nameof(NotificationTemplate.Subject), entity.Subject);
        SetProperty(domain, nameof(NotificationTemplate.Body), entity.Body);
        SetProperty(domain, nameof(NotificationTemplate.IsActive), entity.IsActive);
        SetProperty(domain, nameof(NotificationTemplate.CreatedAt), entity.CreatedAt);
        SetProperty(domain, nameof(NotificationTemplate.UpdatedAt), entity.UpdatedAt);
        return domain;
    }

    public static NotificationTemplateEntity ToEntity(this NotificationTemplate domain)
        => new()
        {
            Id = domain.Id,
            TenantId = domain.TenantId,
            EventType = domain.EventType,
            ChannelType = domain.ChannelType,
            Subject = domain.Subject,
            Body = domain.Body,
            IsActive = domain.IsActive,
            IsDeleted = false,
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
