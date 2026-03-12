using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class TenantPlanEntityMapper
{
    public static TenantPlan ToDomain(TenantPlanEntity entity)
    {
        var domain = new TenantPlan();
        SetProperty(domain, nameof(TenantPlan.Id), entity.Id);
        SetProperty(domain, nameof(TenantPlan.Name), entity.Name);
        SetProperty(domain, nameof(TenantPlan.MaxBookingTypes), entity.MaxBookingTypes);
        SetProperty(domain, nameof(TenantPlan.MaxStaffMembers), entity.MaxStaffMembers);
        SetProperty(domain, nameof(TenantPlan.MaxBookingsPerMonth), entity.MaxBookingsPerMonth);
        SetProperty(domain, nameof(TenantPlan.MaxCustomers), entity.MaxCustomers);
        SetProperty(domain, nameof(TenantPlan.NotificationsEnabled), entity.NotificationsEnabled);
        SetProperty(domain, nameof(TenantPlan.AnalyticsEnabled), entity.AnalyticsEnabled);
        SetProperty(domain, nameof(TenantPlan.CustomBrandingEnabled), entity.CustomBrandingEnabled);
        SetProperty(domain, nameof(TenantPlan.ApiAccessEnabled), entity.ApiAccessEnabled);
        SetProperty(domain, nameof(TenantPlan.AuditLogEnabled), entity.AuditLogEnabled);
        SetProperty(domain, nameof(TenantPlan.PriceCentavos), entity.PriceCentavos);
        SetProperty(domain, nameof(TenantPlan.IsActive), entity.IsActive);
        SetProperty(domain, nameof(TenantPlan.SortOrder), entity.SortOrder);
        SetProperty(domain, nameof(TenantPlan.CreatedAt), entity.CreatedAt);
        return domain;
    }

    public static TenantPlanEntity ToEntity(TenantPlan domain)
        => new()
        {
            Id = domain.Id,
            Name = domain.Name,
            MaxBookingTypes = domain.MaxBookingTypes,
            MaxStaffMembers = domain.MaxStaffMembers,
            MaxBookingsPerMonth = domain.MaxBookingsPerMonth,
            MaxCustomers = domain.MaxCustomers,
            NotificationsEnabled = domain.NotificationsEnabled,
            AnalyticsEnabled = domain.AnalyticsEnabled,
            CustomBrandingEnabled = domain.CustomBrandingEnabled,
            ApiAccessEnabled = domain.ApiAccessEnabled,
            AuditLogEnabled = domain.AuditLogEnabled,
            PriceCentavos = domain.PriceCentavos,
            IsActive = domain.IsActive,
            SortOrder = domain.SortOrder,
            CreatedAt = domain.CreatedAt,
            IsDeleted = false,
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
