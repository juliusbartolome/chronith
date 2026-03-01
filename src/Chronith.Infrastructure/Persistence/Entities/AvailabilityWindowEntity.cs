namespace Chronith.Infrastructure.Persistence.Entities;

/// <summary>
/// Stores a single TimeSlotWindow for a TimeSlotBookingType.
/// </summary>
public sealed class AvailabilityWindowEntity
{
    public Guid Id { get; set; }
    public Guid BookingTypeId { get; set; }

    public int DayOfWeek { get; set; }      // cast to/from System.DayOfWeek
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    // Navigation
    public BookingTypeEntity? BookingType { get; set; }
}
