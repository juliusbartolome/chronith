using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class AuditEntryEntityMapper
{
    public static AuditEntry ToDomain(AuditEntryEntity entity)
    {
        var domain = new AuditEntry();
        SetProperty(domain, nameof(AuditEntry.Id), entity.Id);
        SetProperty(domain, nameof(AuditEntry.TenantId), entity.TenantId);
        SetProperty(domain, nameof(AuditEntry.UserId), entity.UserId);
        SetProperty(domain, nameof(AuditEntry.UserRole), entity.UserRole);
        SetProperty(domain, nameof(AuditEntry.EntityType), entity.EntityType);
        SetProperty(domain, nameof(AuditEntry.EntityId), entity.EntityId);
        SetProperty(domain, nameof(AuditEntry.Action), entity.Action);
        SetProperty(domain, nameof(AuditEntry.OldValues), entity.OldValues);
        SetProperty(domain, nameof(AuditEntry.NewValues), entity.NewValues);
        SetProperty(domain, nameof(AuditEntry.Metadata), entity.Metadata);
        SetProperty(domain, nameof(AuditEntry.Timestamp), entity.Timestamp);
        return domain;
    }

    public static AuditEntryEntity ToEntity(AuditEntry domain) => new()
    {
        Id = domain.Id,
        TenantId = domain.TenantId,
        UserId = domain.UserId,
        UserRole = domain.UserRole,
        EntityType = domain.EntityType,
        EntityId = domain.EntityId,
        Action = domain.Action,
        OldValues = domain.OldValues,
        NewValues = domain.NewValues,
        Metadata = domain.Metadata,
        Timestamp = domain.Timestamp
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
