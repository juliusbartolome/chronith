using Chronith.Domain.Enums;

namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class WaitlistEntryEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BookingTypeId { get; set; }
    public Guid? StaffMemberId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public DateTimeOffset DesiredStart { get; set; }
    public DateTimeOffset DesiredEnd { get; set; }
    public WaitlistStatus Status { get; set; }
    public DateTimeOffset? OfferedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
