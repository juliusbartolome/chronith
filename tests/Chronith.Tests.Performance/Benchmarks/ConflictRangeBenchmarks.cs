using BenchmarkDotNet.Attributes;
using Chronith.Application.Services;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;

namespace Chronith.Tests.Performance.Benchmarks;

/// <summary>
/// Benchmarks for SlotGeneratorService conflict detection at scale.
/// Scenarios:
///   - 10k bookings, overlap found on first check (best case for the overlap scan)
///   - 10k bookings, no overlap at all (worst case — must scan all 10k)
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class ConflictRangeBenchmarks
{
    private SlotGeneratorService _service = null!;
    private TenantTimeZone _tz = null!;
    private TimeSlotBookingType _bt = null!;

    private DateTimeOffset _from;
    private DateTimeOffset _to;

    /// <summary>10k bookings where the very first slot conflicts — overlap found immediately.</summary>
    private IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> _10kConflictFirst = null!;

    /// <summary>10k bookings in a completely different time range — no overlap, full scan required.</summary>
    private IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> _10kNoConflict = null!;

    [GlobalSetup]
    public void Setup()
    {
        _service = new SlotGeneratorService();
        _tz = new TenantTimeZone("UTC");

        var windows = Enum.GetValues<DayOfWeek>()
            .Select(d => new TimeSlotWindow(d, new TimeOnly(8, 0), new TimeOnly(18, 0)))
            .ToList();

        _bt = TimeSlotBookingType.Create(
            tenantId: Guid.NewGuid(),
            slug: "conflict-bench",
            name: "Conflict Bench",
            capacity: 1,
            paymentMode: PaymentMode.Manual,
            paymentProvider: null,
            durationMinutes: 30,
            bufferBeforeMinutes: 0,
            bufferAfterMinutes: 0,
            availabilityWindows: windows);

        // Benchmark window: Monday 2026-03-02, one week
        var monday = new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero);
        _from = monday;
        _to   = monday.AddDays(7);

        // ── Conflict on first check ──
        // First booking starts exactly at the first available slot (Mon 08:00)
        var conflictFirst = new List<(DateTimeOffset, DateTimeOffset)>(10_000);
        // Insert the conflicting booking first so the scan hits it immediately
        conflictFirst.Add((monday.AddHours(8), monday.AddHours(8).AddMinutes(30)));
        // Fill the rest with bookings in a future week (they won't interfere with this week)
        var futureCursor = monday.AddDays(14).AddHours(8);
        for (int i = 1; i < 10_000; i++)
        {
            conflictFirst.Add((futureCursor, futureCursor.AddMinutes(30)));
            futureCursor = futureCursor.AddMinutes(60);
            if (futureCursor.Hour >= 18)
                futureCursor = new DateTimeOffset(futureCursor.Date.AddDays(1).AddHours(8), TimeSpan.Zero);
        }
        _10kConflictFirst = conflictFirst;

        // ── No conflict — must scan all 10k ──
        // All 10k bookings are in a completely different year; none overlap with _from/_to
        var noConflict = new List<(DateTimeOffset, DateTimeOffset)>(10_000);
        var pastCursor = new DateTimeOffset(2020, 1, 6, 8, 0, 0, TimeSpan.Zero); // Mon 2020-01-06
        for (int i = 0; i < 10_000; i++)
        {
            noConflict.Add((pastCursor, pastCursor.AddMinutes(30)));
            pastCursor = pastCursor.AddMinutes(60);
            if (pastCursor.Hour >= 18)
                pastCursor = new DateTimeOffset(pastCursor.Date.AddDays(1).AddHours(8), TimeSpan.Zero);
        }
        _10kNoConflict = noConflict;
    }

    [Benchmark(Description = "10k bookings — conflict on first check")]
    public IReadOnlyList<(DateTimeOffset, DateTimeOffset)> Conflict_10k_FirstHit()
        => _service.GenerateAvailableSlots(_bt, _tz, _from, _to, _10kConflictFirst);

    [Benchmark(Description = "10k bookings — no conflict (worst-case scan)")]
    public IReadOnlyList<(DateTimeOffset, DateTimeOffset)> Conflict_10k_NoHit()
        => _service.GenerateAvailableSlots(_bt, _tz, _from, _to, _10kNoConflict);
}
