using Chronith.Application.Interfaces;
using Chronith.Domain.Models;

namespace Chronith.Application.Services;

/// <summary>
/// Pure C# slot generator — no DB calls.
/// Generates candidate slots from the booking type's weekly windows,
/// then subtracts already-booked slots (using effective ranges for buffers).
/// </summary>
public sealed class SlotGeneratorService : ISlotGeneratorService
{
    public IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> GenerateAvailableSlots(
        BookingType bookingType,
        TenantTimeZone tz,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> bookedSlots)
    {
        return bookingType switch
        {
            TimeSlotBookingType ts => GenerateTimeSlots(ts, tz, from, to, bookedSlots),
            CalendarBookingType cal => GenerateCalendarSlots(cal, tz, from, to, bookedSlots),
            _ => throw new ArgumentOutOfRangeException(nameof(bookingType),
                $"Unknown booking type: {bookingType.GetType().Name}")
        };
    }

    // ────────────────────────────────────────────────────────────────────
    // TimeSlot generation
    // ────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> GenerateTimeSlots(
        TimeSlotBookingType bt,
        TenantTimeZone tz,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> bookedSlots)
    {
        if (bt.AvailabilityWindows.Count == 0)
            return [];

        // Step = duration + bufferBefore + bufferAfter (total slot "footprint" for scheduling)
        var stepMinutes = bt.DurationMinutes + bt.BufferBeforeMinutes + bt.BufferAfterMinutes;

        var result = new List<(DateTimeOffset Start, DateTimeOffset End)>();

        // Walk day-by-day through the range
        var currentDate = DateOnly.FromDateTime(tz.ToLocalDate(from).ToDateTime(TimeOnly.MinValue));
        var endDate = DateOnly.FromDateTime(tz.ToLocalDate(to).ToDateTime(TimeOnly.MinValue));

        while (currentDate <= endDate)
        {
            var dow = currentDate.DayOfWeek;
            foreach (var window in bt.AvailabilityWindows.Where(w => w.DayOfWeek == dow))
            {
                // Generate slots within this window
                var slotStart = tz.ToUtc(currentDate, window.StartTime);

                while (true)
                {
                    var slotEnd = slotStart.AddMinutes(bt.DurationMinutes);

                    // Slot end must not exceed the window end in local time
                    var localSlotEndTime = TimeOnly.FromDateTime(tz.ToLocalDateTime(slotEnd));
                    if (localSlotEndTime > window.EndTime ||
                        (localSlotEndTime == TimeOnly.MinValue && window.EndTime != TimeOnly.MinValue))
                        break;

                    // Slot must be within the requested from/to range
                    if (slotStart >= from && slotEnd <= to)
                    {
                        // Check against booked slots using effective ranges
                        var (effStart, effEnd) = bt.GetEffectiveRange(slotStart, slotEnd);
                        if (!Overlaps(effStart, effEnd, bookedSlots, bt))
                            result.Add((slotStart, slotEnd));
                    }
                    else if (slotStart > to)
                    {
                        break;
                    }

                    slotStart = slotStart.AddMinutes(stepMinutes);
                }
            }

            currentDate = currentDate.AddDays(1);
        }

        return result;
    }

    // ────────────────────────────────────────────────────────────────────
    // Calendar generation
    // ────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> GenerateCalendarSlots(
        CalendarBookingType bt,
        TenantTimeZone tz,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> bookedSlots)
    {
        if (bt.AvailableDays.Count == 0)
            return [];

        var result = new List<(DateTimeOffset Start, DateTimeOffset End)>();

        var currentDate = tz.ToLocalDate(from);
        var endDate = tz.ToLocalDate(to);

        while (currentDate <= endDate)
        {
            if (bt.AvailableDays.Contains(currentDate.DayOfWeek))
            {
                var dayStart = tz.ToUtc(currentDate, TimeOnly.MinValue);
                var dayEnd = tz.ToUtc(currentDate.AddDays(1), TimeOnly.MinValue);

                // Check it's within the requested range
                if (dayStart >= from || dayEnd <= to)
                {
                    // Calendar has no buffers — effective range = actual range
                    if (!IsFullyBooked(dayStart, dayEnd, bookedSlots))
                        result.Add((dayStart, dayEnd));
                }
            }

            currentDate = currentDate.AddDays(1);
        }

        return result;
    }

    // ────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks if the effective range of a candidate slot overlaps with any booked slot's effective range.
    /// </summary>
    private static bool Overlaps(
        DateTimeOffset effStart,
        DateTimeOffset effEnd,
        IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> bookedSlots,
        TimeSlotBookingType bt)
    {
        foreach (var booked in bookedSlots)
        {
            var (bookedEffStart, bookedEffEnd) = bt.GetEffectiveRange(booked.Start, booked.End);
            // Overlap: effStart < bookedEffEnd AND effEnd > bookedEffStart
            if (effStart < bookedEffEnd && effEnd > bookedEffStart)
                return true;
        }
        return false;
    }

    /// <summary>
    /// For calendar bookings: a day is "booked" if any booked slot's range exactly matches
    /// (or contains) the full day range.
    /// </summary>
    private static bool IsFullyBooked(
        DateTimeOffset dayStart,
        DateTimeOffset dayEnd,
        IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> bookedSlots)
    {
        foreach (var booked in bookedSlots)
        {
            // Overlap check: booked.Start < dayEnd AND booked.End > dayStart
            if (booked.Start < dayEnd && booked.End > dayStart)
                return true;
        }
        return false;
    }
}
