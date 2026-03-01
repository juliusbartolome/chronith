namespace Chronith.Domain.Models;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;

public sealed class TimeSlotBookingType : BookingType
{
    public int DurationMinutes { get; private set; }
    public int BufferBeforeMinutes { get; private set; }
    public int BufferAfterMinutes { get; private set; }
    public IReadOnlyList<TimeSlotWindow> AvailabilityWindows { get; private set; }
        = Array.Empty<TimeSlotWindow>();

    // For Infrastructure hydration
    internal TimeSlotBookingType() { }

    public override (DateTimeOffset Start, DateTimeOffset End) ResolveSlot(
        DateTimeOffset requestedStart, TenantTimeZone tz)
    {
        var localDt = tz.ToLocalDateTime(requestedStart);
        var dow = localDt.DayOfWeek;
        var localTime = TimeOnly.FromTimeSpan(localDt.TimeOfDay);

        var slotEnd = localTime.AddMinutes(DurationMinutes);

        var window = AvailabilityWindows.FirstOrDefault(w =>
            w.DayOfWeek == dow &&
            localTime >= w.StartTime &&
            slotEnd <= w.EndTime);

        if (window is null)
            throw new SlotNotInWindowException(requestedStart);

        return (requestedStart, requestedStart.AddMinutes(DurationMinutes));
    }

    public override (DateTimeOffset EffectiveStart, DateTimeOffset EffectiveEnd)
        GetEffectiveRange(DateTimeOffset start, DateTimeOffset end)
        => (start.AddMinutes(-BufferBeforeMinutes), end.AddMinutes(BufferAfterMinutes));
}
