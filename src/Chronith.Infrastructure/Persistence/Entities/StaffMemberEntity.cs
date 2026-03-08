namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class StaffMemberEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? TenantUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public ICollection<StaffAvailabilityWindowEntity> AvailabilityWindows { get; set; } = [];
    public ICollection<BookingTypeStaffAssignmentEntity> BookingTypeAssignments { get; set; } = [];
}
