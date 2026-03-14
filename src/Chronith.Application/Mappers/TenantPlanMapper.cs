using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class TenantPlanMapper
{
    public static TenantPlanDto ToDto(this TenantPlan plan) => new(
        plan.Id,
        plan.Name,
        plan.MaxBookingTypes,
        plan.MaxStaffMembers,
        plan.MaxBookingsPerMonth,
        plan.MaxCustomers,
        plan.NotificationsEnabled,
        plan.AnalyticsEnabled,
        plan.CustomBrandingEnabled,
        plan.ApiAccessEnabled,
        plan.AuditLogEnabled,
        plan.PriceCentavos,
        plan.SortOrder
    );
}
