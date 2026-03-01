using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class TimeSlotResolveSlotTests
{
    // America/New_York in winter = UTC-5
    // We'll use UTC timezone to keep tests simple and predictable
    private static readonly TenantTimeZone UtcTz = new("UTC");

    // Monday window: 09:00–17:00
    private static readonly TimeSlotWindow MondayWindow =
        new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0));

    [Fact]
    public void ResolveSlot_WhenStartFallsInWindow_ReturnsCorrectStartAndEnd()
    {
        // Monday 2026-03-02 10:00 UTC
        var requestedStart = new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows: [MondayWindow]);

        var (start, end) = bt.ResolveSlot(requestedStart, UtcTz);

        start.Should().Be(requestedStart);
        end.Should().Be(requestedStart.AddMinutes(60));
    }

    [Fact]
    public void ResolveSlot_WhenStartBeforeWindowStart_ThrowsSlotNotInWindowException()
    {
        // Monday 2026-03-02 08:00 UTC — before 09:00 window start
        var requestedStart = new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows: [MondayWindow]);

        var act = () => bt.ResolveSlot(requestedStart, UtcTz);

        act.Should().Throw<SlotNotInWindowException>();
    }

    [Fact]
    public void ResolveSlot_WhenStartAfterWindowEnd_ThrowsSlotNotInWindowException()
    {
        // Monday 2026-03-02 17:00 UTC — at window end, so slot would end at 18:00, exceeds window
        var requestedStart = new DateTimeOffset(2026, 3, 2, 17, 0, 0, TimeSpan.Zero);
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows: [MondayWindow]);

        var act = () => bt.ResolveSlot(requestedStart, UtcTz);

        act.Should().Throw<SlotNotInWindowException>();
    }

    [Fact]
    public void ResolveSlot_WhenSlotEndExceedsWindowEnd_ThrowsSlotNotInWindowException()
    {
        // Monday 2026-03-02 16:30 UTC — slot would end 17:30, but window ends at 17:00
        var requestedStart = new DateTimeOffset(2026, 3, 2, 16, 30, 0, TimeSpan.Zero);
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows: [MondayWindow]);

        var act = () => bt.ResolveSlot(requestedStart, UtcTz);

        act.Should().Throw<SlotNotInWindowException>();
    }

    [Fact]
    public void ResolveSlot_WhenDayOfWeekNotInWindows_ThrowsSlotNotInWindowException()
    {
        // Tuesday 2026-03-03 10:00 UTC — no Tuesday window
        var requestedStart = new DateTimeOffset(2026, 3, 3, 10, 0, 0, TimeSpan.Zero);
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows: [MondayWindow]);

        var act = () => bt.ResolveSlot(requestedStart, UtcTz);

        act.Should().Throw<SlotNotInWindowException>();
    }

    [Fact]
    public void ResolveSlot_EndIsStart_PlusDurationMinutes()
    {
        // Monday 2026-03-02 09:00 UTC — slot of 30 minutes
        var requestedStart = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 30,
            windows: [new TimeSlotWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))]);

        var (start, end) = bt.ResolveSlot(requestedStart, UtcTz);

        end.Should().Be(start.AddMinutes(30));
    }
}
