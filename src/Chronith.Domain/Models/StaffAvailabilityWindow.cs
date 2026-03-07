namespace Chronith.Domain.Models;

public sealed record StaffAvailabilityWindow(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime)
{
    public StaffAvailabilityWindow() : this(default, default, default) { }
}
