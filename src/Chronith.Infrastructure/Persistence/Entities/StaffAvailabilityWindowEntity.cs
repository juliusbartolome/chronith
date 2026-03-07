namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class StaffAvailabilityWindowEntity
{
    public Guid Id { get; set; }
    public Guid StaffMemberId { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    // Navigation
    public StaffMemberEntity? StaffMember { get; set; }
}
