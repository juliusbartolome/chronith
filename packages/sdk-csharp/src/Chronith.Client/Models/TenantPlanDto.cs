namespace Chronith.Client.Models;

public sealed record TenantPlanDto(
    Guid Id,
    string Name,
    int MaxBookingTypes,
    int MaxStaffMembers,
    int MaxBookingsPerMonth,
    int MaxCustomers,
    bool NotificationsEnabled,
    bool AnalyticsEnabled,
    bool CustomBrandingEnabled,
    bool ApiAccessEnabled,
    bool AuditLogEnabled,
    long PriceCentavos,
    int SortOrder
);
