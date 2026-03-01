using Chronith.Application.Services;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Unit.Application;

public sealed class SlotGeneratorServiceTests
{
    private static readonly TenantTimeZone Utc = new("UTC");
    private static readonly TenantTimeZone Eastern = new("America/New_York");
    private readonly SlotGeneratorService _sut = new();

    // ──────────────────────────────────────────────────────────────────────
    // TimeSlot: single window
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void TimeSlot_SingleWindow_NoBookings_ProducesCorrectSlots()
    {
        // Window: Monday 09:00–11:00, 60-min duration, no buffers
        // From Monday 2026-01-05 00:00 UTC to Monday 2026-01-05 23:59 UTC
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows:
            [
                new TimeSlotWindow(DayOfWeek.Monday,
                    new TimeOnly(9, 0), new TimeOnly(11, 0))
            ]);

        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 5, 23, 59, 0, TimeSpan.Zero);

        var slots = _sut.GenerateAvailableSlots(bt, Utc, from, to, []);

        // Expect: 09:00–10:00 and 10:00–11:00
        slots.Should().HaveCount(2);
        slots[0].Start.Should().Be(new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero));
        slots[0].End.Should().Be(new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero));
        slots[1].Start.Should().Be(new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero));
        slots[1].End.Should().Be(new DateTimeOffset(2026, 1, 5, 11, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void TimeSlot_BufferBefore_And_After_AdvancesSlotStepCorrectly()
    {
        // Window: Monday 09:00–13:00, 60-min duration, 15-min buffer before, 15-min buffer after
        // Step = 60 + 15 + 15 = 90 min between slot starts
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            bufferBeforeMinutes: 15,
            bufferAfterMinutes: 15,
            windows:
            [
                new TimeSlotWindow(DayOfWeek.Monday,
                    new TimeOnly(9, 0), new TimeOnly(13, 0))
            ]);

        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 5, 23, 59, 0, TimeSpan.Zero);

        var slots = _sut.GenerateAvailableSlots(bt, Utc, from, to, []);

        // 09:00 → 10:00, next start = 10:00 + bufferAfter(15) + bufferBefore(15) = 10:30
        // Actually step from start-to-start = duration + bufferAfter + bufferBefore = 60+15+15 = 90
        // Slots: 09:00, 10:30, 12:00 — but 12:00 end is 13:00 which fits window end exactly
        slots.Should().HaveCount(3);
        slots[0].Start.Should().Be(new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero));
        slots[1].Start.Should().Be(new DateTimeOffset(2026, 1, 5, 10, 30, 0, TimeSpan.Zero));
        slots[2].Start.Should().Be(new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void TimeSlot_ExcludesSlotsThatExceedWindowEnd()
    {
        // Window: Monday 09:00–10:30, 60-min slots, no buffers
        // Slot at 09:00 fits (ends at 10:00). Slot at 10:00 doesn't fit (ends at 11:00 > 10:30)
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows:
            [
                new TimeSlotWindow(DayOfWeek.Monday,
                    new TimeOnly(9, 0), new TimeOnly(10, 30))
            ]);

        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 5, 23, 59, 0, TimeSpan.Zero);

        var slots = _sut.GenerateAvailableSlots(bt, Utc, from, to, []);

        slots.Should().HaveCount(1);
        slots[0].Start.Should().Be(new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void TimeSlot_AcrossMultipleDays_ReturnsAllDays()
    {
        // Window on Monday and Wednesday, 60-min slots
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows:
            [
                new TimeSlotWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0)),
                new TimeSlotWindow(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(10, 0))
            ]);

        // Mon 2026-01-05 to Wed 2026-01-07
        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 7, 23, 59, 0, TimeSpan.Zero);

        var slots = _sut.GenerateAvailableSlots(bt, Utc, from, to, []);

        // 1 slot Monday, 0 Tuesday, 1 Wednesday
        slots.Should().HaveCount(2);
        slots[0].Start.DayOfWeek.Should().Be(DayOfWeek.Monday);
        slots[1].Start.DayOfWeek.Should().Be(DayOfWeek.Wednesday);
    }

    [Fact]
    public void TimeSlot_SkipsDaysNotInWindow()
    {
        // Only Tuesday window but from is Monday–Wednesday
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows:
            [
                new TimeSlotWindow(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(10, 0))
            ]);

        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero); // Monday
        var to = new DateTimeOffset(2026, 1, 7, 23, 59, 0, TimeSpan.Zero); // Wednesday

        var slots = _sut.GenerateAvailableSlots(bt, Utc, from, to, []);

        slots.Should().HaveCount(1);
        slots[0].Start.DayOfWeek.Should().Be(DayOfWeek.Tuesday);
    }

    [Fact]
    public void TimeSlot_RespectsFromAndToBoundary()
    {
        // Window: Mon–Fri 09:00–17:00, but from/to only covers part of Monday
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows:
            [
                new TimeSlotWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0))
            ]);

        // Only allow slots between 11:00 and 13:00 on Monday
        var from = new DateTimeOffset(2026, 1, 5, 11, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 5, 13, 0, 0, TimeSpan.Zero);

        var slots = _sut.GenerateAvailableSlots(bt, Utc, from, to, []);

        // 11:00–12:00 and 12:00–13:00
        slots.Should().HaveCount(2);
        slots[0].Start.Should().Be(new DateTimeOffset(2026, 1, 5, 11, 0, 0, TimeSpan.Zero));
        slots[1].Start.Should().Be(new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero));
    }

    // ──────────────────────────────────────────────────────────────────────
    // TimeSlot: booked slot subtraction
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void TimeSlot_ExcludesBookedSlots()
    {
        // Window: Monday 09:00–11:00, 60-min, no buffers
        // 09:00–10:00 already booked
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows:
            [
                new TimeSlotWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0))
            ]);

        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 5, 23, 59, 0, TimeSpan.Zero);
        var booked = new[]
        {
            (Start: new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero),
             End: new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero))
        };

        var slots = _sut.GenerateAvailableSlots(bt, Utc, from, to, booked);

        slots.Should().HaveCount(1);
        slots[0].Start.Should().Be(new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void TimeSlot_WithBuffers_BookedSlotBlocksBufferAdjustedRange()
    {
        // 60-min duration, 15-min buffer before/after
        // Step = 90 min. Slots at 09:00, 10:30, 12:00 (in 09:00–13:00 window)
        // If 09:00–10:00 is booked, effective range is 08:45–10:15
        // So 10:30–11:30 slot has effective range 10:15–11:45 — overlaps with 08:45–10:15? No.
        // But 10:30 - bufferBefore(15) = 10:15, which is after 10:15 (the effective end of booked)
        // So 10:30 should NOT be blocked. Only 09:00 is blocked.
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            bufferBeforeMinutes: 15,
            bufferAfterMinutes: 15,
            windows:
            [
                new TimeSlotWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0))
            ]);

        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 5, 23, 59, 0, TimeSpan.Zero);
        var booked = new[]
        {
            (Start: new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero),
             End: new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero))
        };

        var slots = _sut.GenerateAvailableSlots(bt, Utc, from, to, booked);

        // 09:00 blocked, 10:30 and 12:00 available
        slots.Should().HaveCount(2);
        slots[0].Start.Should().Be(new DateTimeOffset(2026, 1, 5, 10, 30, 0, TimeSpan.Zero));
        slots[1].Start.Should().Be(new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Calendar: slot generation
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Calendar_ReturnsOneDayPerAvailableDay()
    {
        // Mon + Wed available, range covers Mon–Fri
        var bt = BookingTypeBuilder.BuildCalendar(
            availableDays: [DayOfWeek.Monday, DayOfWeek.Wednesday]);

        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero); // Mon
        var to = new DateTimeOffset(2026, 1, 9, 23, 59, 0, TimeSpan.Zero); // Fri

        var slots = _sut.GenerateAvailableSlots(bt, Utc, from, to, []);

        slots.Should().HaveCount(2);
    }

    [Fact]
    public void Calendar_SkipsUnavailableDaysOfWeek()
    {
        var bt = BookingTypeBuilder.BuildCalendar(
            availableDays: [DayOfWeek.Friday]);

        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero); // Mon
        var to = new DateTimeOffset(2026, 1, 9, 23, 59, 0, TimeSpan.Zero); // Fri

        var slots = _sut.GenerateAvailableSlots(bt, Utc, from, to, []);

        slots.Should().HaveCount(1);
        slots[0].Start.DayOfWeek.Should().Be(DayOfWeek.Friday);
    }

    [Fact]
    public void Calendar_ProducesFullDayBoundsInUtc()
    {
        // UTC timezone: Monday 2026-01-05
        var bt = BookingTypeBuilder.BuildCalendar(
            availableDays: [DayOfWeek.Monday]);

        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 5, 23, 59, 0, TimeSpan.Zero);

        var slots = _sut.GenerateAvailableSlots(bt, Utc, from, to, []);

        slots.Should().HaveCount(1);
        // Full UTC day: midnight to midnight
        slots[0].Start.Should().Be(new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero));
        slots[0].End.Should().Be(new DateTimeOffset(2026, 1, 6, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Calendar_DayBoundariesCorrect_InNonUtcTimezone()
    {
        // Eastern (UTC-5 in January): Monday 2026-01-05 starts at 05:00 UTC, ends at 2026-01-06 05:00 UTC
        var bt = BookingTypeBuilder.BuildCalendar(
            availableDays: [DayOfWeek.Monday]);

        // from/to in UTC — but we're looking for Eastern Monday
        // Eastern Monday 2026-01-05 = UTC 2026-01-05T05:00Z to 2026-01-06T05:00Z
        var from = new DateTimeOffset(2026, 1, 5, 5, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 5, 23, 59, 0, TimeSpan.Zero);

        var slots = _sut.GenerateAvailableSlots(bt, Eastern, from, to, []);

        slots.Should().HaveCount(1);
        slots[0].Start.Should().Be(new DateTimeOffset(2026, 1, 5, 5, 0, 0, TimeSpan.Zero));   // 00:00 Eastern = 05:00 UTC
        slots[0].End.Should().Be(new DateTimeOffset(2026, 1, 6, 5, 0, 0, TimeSpan.Zero));     // 00:00 Eastern next day = 05:00 UTC
    }

    [Fact]
    public void Calendar_RespectsFromAndToBoundary()
    {
        // All days available but range only covers 3 days
        var bt = BookingTypeBuilder.BuildCalendar(
            availableDays:
            [
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday
            ]);

        var from = new DateTimeOffset(2026, 1, 6, 0, 0, 0, TimeSpan.Zero); // Tue
        var to = new DateTimeOffset(2026, 1, 8, 23, 59, 0, TimeSpan.Zero); // Thu

        var slots = _sut.GenerateAvailableSlots(bt, Utc, from, to, []);

        slots.Should().HaveCount(3);
    }

    [Fact]
    public void Calendar_ExcludesBookedDays()
    {
        // Mon + Tue + Wed available, Tuesday is fully booked
        var bt = BookingTypeBuilder.BuildCalendar(
            availableDays: [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday]);

        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero); // Mon
        var to = new DateTimeOffset(2026, 1, 7, 23, 59, 0, TimeSpan.Zero); // Wed

        // Tuesday booked (full day in UTC)
        var booked = new[]
        {
            (Start: new DateTimeOffset(2026, 1, 6, 0, 0, 0, TimeSpan.Zero),
             End: new DateTimeOffset(2026, 1, 7, 0, 0, 0, TimeSpan.Zero))
        };

        var slots = _sut.GenerateAvailableSlots(bt, Utc, from, to, booked);

        slots.Should().HaveCount(2);
        slots[0].Start.DayOfWeek.Should().Be(DayOfWeek.Monday);
        slots[1].Start.DayOfWeek.Should().Be(DayOfWeek.Wednesday);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Edge: empty windows / available days
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void TimeSlot_NoWindows_ReturnsEmpty()
    {
        var bt = BookingTypeBuilder.BuildTimeSlot(durationMinutes: 60, windows: []);
        var slots = _sut.GenerateAvailableSlots(
            bt, Utc,
            new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 5, 23, 59, 0, TimeSpan.Zero),
            []);
        slots.Should().BeEmpty();
    }

    [Fact]
    public void Calendar_NoAvailableDays_ReturnsEmpty()
    {
        var bt = BookingTypeBuilder.BuildCalendar(availableDays: []);
        var slots = _sut.GenerateAvailableSlots(
            bt, Utc,
            new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 11, 23, 59, 0, TimeSpan.Zero),
            []);
        slots.Should().BeEmpty();
    }
}
