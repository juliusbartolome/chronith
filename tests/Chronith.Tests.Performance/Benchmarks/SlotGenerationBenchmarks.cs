using BenchmarkDotNet.Attributes;
using Chronith.Application.Services;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;

namespace Chronith.Tests.Performance.Benchmarks;

/// <summary>
/// Benchmarks for SlotGeneratorService.GenerateAvailableSlots().
/// Scenarios:
///   - TimeSlot, 1-week window, 0 bookings
///   - TimeSlot, 1-week window, 500 bookings
///   - Calendar, 1-week window
///   - Calendar, 30-day window
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class SlotGenerationBenchmarks
{
    private SlotGeneratorService _service = null!;
    private TenantTimeZone _tz = null!;

    private TimeSlotBookingType _timeSlotBt = null!;
    private CalendarBookingType _calendarBt = null!;

    private DateTimeOffset _weekFrom;
    private DateTimeOffset _weekTo;
    private DateTimeOffset _monthFrom;
    private DateTimeOffset _monthTo;

    private IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> _noBookings = [];
    private IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> _500Bookings = null!;

    [GlobalSetup]
    public void Setup()
    {
        _service = new SlotGeneratorService();
        _tz = new TenantTimeZone("UTC");

        // 30-minute slots, Mon–Sun 08:00–18:00, no buffers
        var windows = Enum.GetValues<DayOfWeek>()
            .Select(d => new TimeSlotWindow(d, new TimeOnly(8, 0), new TimeOnly(18, 0)))
            .ToList();

        _timeSlotBt = TimeSlotBookingType.Create(
            tenantId: Guid.NewGuid(),
            slug: "bench-timeslot",
            name: "Bench TimeSlot",
            capacity: 1,
            paymentMode: PaymentMode.Manual,
            paymentProvider: null,
            durationMinutes: 30,
            bufferBeforeMinutes: 0,
            bufferAfterMinutes: 0,
            availabilityWindows: windows);

        _calendarBt = CalendarBookingType.Create(
            tenantId: Guid.NewGuid(),
            slug: "bench-calendar",
            name: "Bench Calendar",
            capacity: 1,
            paymentMode: PaymentMode.Manual,
            paymentProvider: null,
            availableDays: [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                            DayOfWeek.Thursday, DayOfWeek.Friday]);

        // Anchor to a fixed Monday UTC to keep benchmarks deterministic
        var monday = new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero); // Mon 2026-03-02

        _weekFrom  = monday;
        _weekTo    = monday.AddDays(7);
        _monthFrom = monday;
        _monthTo   = monday.AddDays(30);

        // Pre-build 500 booked slots spread across the week (every other 30-min slot on Monday)
        var booked = new List<(DateTimeOffset, DateTimeOffset)>(500);
        var cursor = monday.AddHours(8); // 08:00 UTC Monday
        for (int i = 0; i < 500; i++)
        {
            booked.Add((cursor, cursor.AddMinutes(30)));
            cursor = cursor.AddMinutes(60); // every other slot — skip a slot between each booking
            // Wrap around to next day if we go past 18:00
            var localHour = cursor.Hour;
            if (localHour >= 18)
                cursor = new DateTimeOffset(cursor.Date.AddDays(1).AddHours(8), TimeSpan.Zero);
        }
        _500Bookings = booked;
    }

    [Benchmark(Description = "TimeSlot 1-week / 0 bookings")]
    public IReadOnlyList<(DateTimeOffset, DateTimeOffset)> TimeSlot_1Week_NoBookings()
        => _service.GenerateAvailableSlots(_timeSlotBt, _tz, _weekFrom, _weekTo, _noBookings);

    [Benchmark(Description = "TimeSlot 1-week / 500 bookings")]
    public IReadOnlyList<(DateTimeOffset, DateTimeOffset)> TimeSlot_1Week_500Bookings()
        => _service.GenerateAvailableSlots(_timeSlotBt, _tz, _weekFrom, _weekTo, _500Bookings);

    [Benchmark(Description = "Calendar 1-week (Mon–Fri)")]
    public IReadOnlyList<(DateTimeOffset, DateTimeOffset)> Calendar_1Week()
        => _service.GenerateAvailableSlots(_calendarBt, _tz, _weekFrom, _weekTo, _noBookings);

    [Benchmark(Description = "Calendar 30-day (Mon–Fri)")]
    public IReadOnlyList<(DateTimeOffset, DateTimeOffset)> Calendar_30Day()
        => _service.GenerateAvailableSlots(_calendarBt, _tz, _monthFrom, _monthTo, _noBookings);
}
