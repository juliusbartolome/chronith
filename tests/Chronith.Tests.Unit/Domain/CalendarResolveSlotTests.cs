using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class CalendarResolveSlotTests
{
    // UTC timezone for simple predictable tests
    private static readonly TenantTimeZone UtcTz = new("UTC");

    [Fact]
    public void ResolveSlot_WhenDayIsAvailable_ReturnsDayBoundariesInUtc()
    {
        // Sunday 2026-03-01 12:00 UTC — mid-day; should return full day bounds
        var requestedStart = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var bt = BookingTypeBuilder.BuildCalendar(
            availableDays: [DayOfWeek.Sunday]);

        var (start, end) = bt.ResolveSlot(requestedStart, UtcTz);

        // Day start = 2026-03-01 00:00:00 UTC
        start.Should().Be(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        // Day end = 2026-03-02 00:00:00 UTC
        end.Should().Be(new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ResolveSlot_WhenDayIsNotAvailable_ThrowsSlotNotInWindowException()
    {
        // Monday 2026-03-02 10:00 UTC — only Sunday available
        var requestedStart = new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);
        var bt = BookingTypeBuilder.BuildCalendar(
            availableDays: [DayOfWeek.Sunday]);

        var act = () => bt.ResolveSlot(requestedStart, UtcTz);

        act.Should().Throw<SlotNotInWindowException>();
    }

    [Fact]
    public void ResolveSlot_StartIsLocalMidnight_InUtc()
    {
        // Monday 2026-03-02 10:00 UTC
        var requestedStart = new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);
        var bt = BookingTypeBuilder.BuildCalendar(
            availableDays: [DayOfWeek.Monday]);

        var (start, _) = bt.ResolveSlot(requestedStart, UtcTz);

        // In UTC, midnight of 2026-03-02
        start.Should().Be(new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero));
        start.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ResolveSlot_EndIsNextDayMidnight_InUtc()
    {
        // Monday 2026-03-02 10:00 UTC
        var requestedStart = new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);
        var bt = BookingTypeBuilder.BuildCalendar(
            availableDays: [DayOfWeek.Monday]);

        var (_, end) = bt.ResolveSlot(requestedStart, UtcTz);

        // End = Tuesday midnight = 2026-03-03 00:00:00 UTC
        end.Should().Be(new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero));
    }
}
