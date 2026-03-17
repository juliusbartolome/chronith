namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class BookingTypeStaffAssignmentEntity
{
    public Guid BookingTypeId { get; set; }
    public Guid StaffMemberId { get; set; }

    // Navigation
    public BookingTypeEntity? BookingType { get; set; }
    public StaffMemberEntity? StaffMember { get; set; }
}
