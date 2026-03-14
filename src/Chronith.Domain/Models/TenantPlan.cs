namespace Chronith.Domain.Models;

public sealed class TenantPlan
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int MaxBookingTypes { get; private set; }
    public int MaxStaffMembers { get; private set; }
    public int MaxBookingsPerMonth { get; private set; }
    public int MaxCustomers { get; private set; }
    public bool NotificationsEnabled { get; private set; }
    public bool AnalyticsEnabled { get; private set; }
    public bool CustomBrandingEnabled { get; private set; }
    public bool ApiAccessEnabled { get; private set; }
    public bool AuditLogEnabled { get; private set; }
    public long PriceCentavos { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static TenantPlan Create(
        string name,
        int maxBookingTypes,
        int maxStaffMembers,
        int maxBookingsPerMonth,
        int maxCustomers,
        bool notificationsEnabled,
        bool analyticsEnabled,
        bool customBrandingEnabled,
        bool apiAccessEnabled,
        bool auditLogEnabled,
        long priceCentavos,
        int sortOrder)
    {
        return new TenantPlan
        {
            Id = Guid.NewGuid(),
            Name = name,
            MaxBookingTypes = maxBookingTypes,
            MaxStaffMembers = maxStaffMembers,
            MaxBookingsPerMonth = maxBookingsPerMonth,
            MaxCustomers = maxCustomers,
            NotificationsEnabled = notificationsEnabled,
            AnalyticsEnabled = analyticsEnabled,
            CustomBrandingEnabled = customBrandingEnabled,
            ApiAccessEnabled = apiAccessEnabled,
            AuditLogEnabled = auditLogEnabled,
            PriceCentavos = priceCentavos,
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    internal TenantPlan() { } // EF Core hydration only

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
