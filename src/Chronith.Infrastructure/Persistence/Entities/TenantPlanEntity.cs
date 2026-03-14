namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class TenantPlanEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MaxBookingTypes { get; set; }
    public int MaxStaffMembers { get; set; }
    public int MaxBookingsPerMonth { get; set; }
    public int MaxCustomers { get; set; }
    public bool NotificationsEnabled { get; set; }
    public bool AnalyticsEnabled { get; set; }
    public bool CustomBrandingEnabled { get; set; }
    public bool ApiAccessEnabled { get; set; }
    public bool AuditLogEnabled { get; set; }
    public long PriceCentavos { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public uint RowVersion { get; set; }
}
