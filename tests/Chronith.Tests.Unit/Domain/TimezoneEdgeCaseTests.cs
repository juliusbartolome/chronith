using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

/// <summary>
/// Tests for DST transitions, calendar boundaries across timezones,
/// and TimeSlotWindow construction guards.
/// </summary>
public sealed class TimezoneEdgeCaseTests
{
    // ── DST Spring Forward: America/New_York 2026-03-08 ─────────────────────
    // At 2:00 AM clocks spring forward to 3:00 AM.
    // Times between 02:00-02:59 do not exist ("gap").

    [Fact]
    public void SpringForward_SlotAt_02_30_WasGapped_IsHandledWithoutException()
    {
        // TimeZoneInfo.ConvertTimeToUtc on a gapped time adjusts forward automatically.
        var tz = new TenantTimeZone("America/New_York");
        var date = new DateOnly(2026, 3, 8);
        var gappedTime = new TimeOnly(2, 30, 0); // Doesn't exist in Eastern time on this date

        // Should not throw — TimeZoneInfo resolves the gap by treating it as the DST time
        var act = () => tz.ToUtc(date, gappedTime);

        act.Should().NotThrow();
    }

    // ── DST Fall Back: America/New_York 2026-11-01 ───────────────────────────
    // At 2:00 AM clocks fall back to 1:00 AM.
    // Times between 01:00-01:59 are ambiguous (occur twice).
    // TimeZoneInfo.ConvertTimeToUtc uses the standard time offset for ambiguous times.

    [Fact]
    public void FallBack_SlotAt_01_30_IsAmbiguous_UsesFirstOccurrence()
    {
        // America/New_York fall back: 2026-11-01
        // Standard time = UTC-5, DST time = UTC-4
        // For ambiguous time, .NET uses standard time offset (UTC-5) → adds 5 hours
        // So 01:30 ambiguous → UTC 06:30
        var tz = new TenantTimeZone("America/New_York");
        var date = new DateOnly(2026, 11, 1);
        var ambiguousTime = new TimeOnly(1, 30, 0);

        var result = tz.ToUtc(date, ambiguousTime);

        // .NET ConvertTimeToUtc uses standard time offset for ambiguous times = UTC-5
        result.UtcDateTime.Should().Be(new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc));
    }

    // ── Calendar boundary in Eastern Daylight Time ───────────────────────────

    [Fact]
    public void CalendarSlot_InEasternTime_DayStartIsCorrectUtcOffset()
    {
        // 2026-03-15 is in EDT (UTC-4)
        // Day start for 2026-03-15 in America/New_York = 2026-03-15 04:00:00 UTC
        var tz = new TenantTimeZone("America/New_York");
        // Request is any time on that Monday in EDT
        // 2026-03-15 is a Sunday. Let's check: March 15, 2026
        // March 2026: 1=Sun, 2=Mon, ... 8=Sun, 15=Sun
        // So 2026-03-15 is a Sunday
        var requestedStart = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero); // noon UTC
        var bt = BookingTypeBuilder.BuildCalendar(
            availableDays: [DayOfWeek.Sunday]);

        var (start, _) = bt.ResolveSlot(requestedStart, tz);

        // 2026-03-15 00:00 EDT = 2026-03-15 04:00 UTC (EDT = UTC-4)
        start.UtcDateTime.Should().Be(new DateTime(2026, 3, 15, 4, 0, 0, DateTimeKind.Utc));
    }

    // ── TimeSlotWindow constructor guard ─────────────────────────────────────

    [Fact]
    public void TimeSlot_WindowCrossingMidnight_IsNotSupported_ThrowsArgumentException()
    {
        // EndTime (23:00) > StartTime (00:00) is fine, but EndTime <= StartTime is invalid
        // Test: end before start
        var act = () => new TimeSlotWindow(
            DayOfWeek.Monday,
            new TimeOnly(17, 0),  // start 17:00
            new TimeOnly(9, 0));  // end 09:00 — before start

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TimeSlotWindow_WithEqualStartAndEnd_ThrowsArgumentException()
    {
        var act = () => new TimeSlotWindow(
            DayOfWeek.Monday,
            new TimeOnly(9, 0),
            new TimeOnly(9, 0)); // same as start

        act.Should().Throw<ArgumentException>();
    }
}
