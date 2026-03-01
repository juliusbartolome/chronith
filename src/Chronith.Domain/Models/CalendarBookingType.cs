namespace Chronith.Domain.Models;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;

public sealed class CalendarBookingType : BookingType
{
    public IReadOnlyList<DayOfWeek> AvailableDays { get; private set; }
        = Array.Empty<DayOfWeek>();

    // For Infrastructure hydration
    internal CalendarBookingType() { }

    public override (DateTimeOffset Start, DateTimeOffset End) ResolveSlot(
        DateTimeOffset requestedStart, TenantTimeZone tz)
    {
        var localDate = tz.ToLocalDate(requestedStart);
        if (!AvailableDays.Contains(localDate.DayOfWeek))
            throw new SlotNotInWindowException(requestedStart);

        var dayStart = tz.ToUtc(localDate, TimeOnly.MinValue);
        var dayEnd   = tz.ToUtc(localDate.AddDays(1), TimeOnly.MinValue);
        return (dayStart, dayEnd);
    }

    public override (DateTimeOffset EffectiveStart, DateTimeOffset EffectiveEnd)
        GetEffectiveRange(DateTimeOffset start, DateTimeOffset end)
        => (start, end); // No buffers
}
