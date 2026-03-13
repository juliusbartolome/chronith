namespace Chronith.Client.Models;

public sealed record TenantPlanDto(
    Guid Id,
    string Name,
    string Tier,
    long MonthlyPriceCentavos,
    int MaxBookingTypes,
    int MaxStaffMembers,
    int MaxBookingsPerMonth,
    bool HasCustomDomain,
    bool HasAnalytics,
    bool HasAuditLog,
    bool HasApiAccess,
    bool IsActive
);
