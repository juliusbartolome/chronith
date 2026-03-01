using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class TenantEntityMapper
{
    public static Tenant ToDomain(TenantEntity entity)
    {
        var domain = new Tenant();
        SetPrivate(domain, nameof(Tenant.Id), entity.Id);
        SetPrivate(domain, nameof(Tenant.Slug), entity.Slug);
        SetPrivate(domain, nameof(Tenant.Name), entity.Name);
        SetPrivate(domain, nameof(Tenant.TimeZoneId), entity.TimeZoneId);
        SetPrivate(domain, nameof(Tenant.IsDeleted), entity.IsDeleted);
        SetPrivate(domain, nameof(Tenant.CreatedAt), entity.CreatedAt);
        return domain;
    }

    public static TenantEntity ToEntity(Tenant domain)
        => new TenantEntity
        {
            Id = domain.Id,
            Slug = domain.Slug,
            Name = domain.Name,
            TimeZoneId = domain.TimeZoneId,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt
        };

    private static void SetPrivate(object target, string propertyName, object? value)
    {
        var prop = target.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        prop?.SetValue(target, value);
    }
}
