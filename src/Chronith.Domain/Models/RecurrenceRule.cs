using Chronith.Domain.Enums;

namespace Chronith.Domain.Models;

public sealed class RecurrenceRule
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BookingTypeId { get; private set; }
    public RecurrenceFrequency Frequency { get; private set; }
    public int Interval { get; private set; } = 1;
    public IReadOnlyList<DayOfWeek>? DaysOfWeek { get; private set; }
    public DateOnly SeriesStart { get; private set; }
    public DateOnly? SeriesEnd { get; private set; }
    public int? MaxOccurrences { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal RecurrenceRule() { }

    private const int MaxSafetyOccurrences = 10_000;

    public static RecurrenceRule Create(
        Guid tenantId,
        Guid bookingTypeId,
        RecurrenceFrequency frequency,
        int interval,
        IReadOnlyList<DayOfWeek>? daysOfWeek,
        DateOnly seriesStart,
        DateOnly? seriesEnd,
        int? maxOccurrences)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, 1);

        if (seriesEnd.HasValue && seriesEnd.Value < seriesStart)
            throw new ArgumentException("SeriesEnd cannot be before SeriesStart.", nameof(seriesEnd));

        return new RecurrenceRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BookingTypeId = bookingTypeId,
            Frequency = frequency,
            Interval = interval,
            DaysOfWeek = daysOfWeek,
            SeriesStart = seriesStart,
            SeriesEnd = seriesEnd,
            MaxOccurrences = maxOccurrences,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    internal static RecurrenceRule Hydrate(
        Guid id,
        Guid tenantId,
        Guid bookingTypeId,
        RecurrenceFrequency frequency,
        int interval,
        IReadOnlyList<DayOfWeek>? daysOfWeek,
        DateOnly seriesStart,
        DateOnly? seriesEnd,
        int? maxOccurrences,
        bool isDeleted,
        DateTimeOffset createdAt) => new()
    {
        Id = id,
        TenantId = tenantId,
        BookingTypeId = bookingTypeId,
        Frequency = frequency,
        Interval = interval,
        DaysOfWeek = daysOfWeek,
        SeriesStart = seriesStart,
        SeriesEnd = seriesEnd,
        MaxOccurrences = maxOccurrences,
        IsDeleted = isDeleted,
        CreatedAt = createdAt
    };

    public void SoftDelete() => IsDeleted = true;

    /// <summary>
    /// Computes occurrence dates within the given [from, to] range,
    /// respecting SeriesEnd and MaxOccurrences bounds.
    /// </summary>
    public IReadOnlyList<DateOnly> ComputeOccurrences(DateOnly from, DateOnly to)
    {
        var results = new List<DateOnly>();
        var effectiveEnd = to;
        if (SeriesEnd.HasValue && SeriesEnd.Value < effectiveEnd)
            effectiveEnd = SeriesEnd.Value;

        var totalEmitted = 0;

        switch (Frequency)
        {
            case RecurrenceFrequency.Daily:
            {
                var cursor = SeriesStart;
                while (cursor <= effectiveEnd)
                {
                    if (MaxOccurrences.HasValue && totalEmitted >= MaxOccurrences.Value)
                        break;
                    if (totalEmitted >= MaxSafetyOccurrences)
                        break;

                    if (cursor >= from)
                        results.Add(cursor);

                    totalEmitted++;
                    cursor = cursor.AddDays(Interval);
                }

                break;
            }

            case RecurrenceFrequency.Weekly:
            {
                var weekStart = SeriesStart;
                while (weekStart <= effectiveEnd)
                {
                    if (totalEmitted >= MaxSafetyOccurrences)
                        break;

                    if (DaysOfWeek is { Count: > 0 })
                    {
                        foreach (var day in DaysOfWeek.OrderBy(d => d))
                        {
                            var candidate = GetDateForDayOfWeek(weekStart, day);
                            if (candidate < SeriesStart || candidate > effectiveEnd)
                                continue;
                            if (MaxOccurrences.HasValue && totalEmitted >= MaxOccurrences.Value)
                                break;

                            if (candidate >= from)
                                results.Add(candidate);

                            totalEmitted++;
                        }
                    }
                    else
                    {
                        if (MaxOccurrences.HasValue && totalEmitted >= MaxOccurrences.Value)
                            break;

                        if (weekStart >= from && weekStart <= effectiveEnd)
                            results.Add(weekStart);

                        totalEmitted++;
                    }

                    weekStart = weekStart.AddDays(Interval * 7);
                }

                break;
            }

            case RecurrenceFrequency.Monthly:
            {
                var monthOffset = 0;
                while (true)
                {
                    var targetMonth = SeriesStart.AddMonths(monthOffset);
                    var daysInMonth = DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month);
                    var day = Math.Min(SeriesStart.Day, daysInMonth);
                    var candidate = new DateOnly(targetMonth.Year, targetMonth.Month, day);

                    if (candidate > effectiveEnd)
                        break;
                    if (MaxOccurrences.HasValue && totalEmitted >= MaxOccurrences.Value)
                        break;
                    if (totalEmitted >= MaxSafetyOccurrences)
                        break;

                    if (candidate >= from)
                        results.Add(candidate);

                    totalEmitted++;
                    monthOffset += Interval;
                }

                break;
            }
        }

        return results;
    }

    private static DateOnly GetDateForDayOfWeek(DateOnly weekStart, DayOfWeek targetDay)
    {
        var currentDay = weekStart.DayOfWeek;
        var diff = ((int)targetDay - (int)currentDay + 7) % 7;
        return weekStart.AddDays(diff);
    }
}
