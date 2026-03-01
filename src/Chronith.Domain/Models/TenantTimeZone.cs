namespace Chronith.Domain.Models;

public sealed class TenantTimeZone
{
    public string IanaId { get; }
    private readonly TimeZoneInfo _tz;

    public TenantTimeZone(string ianaId)
    {
        IanaId = ianaId;
        _tz = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
    }

    /// <summary>
    /// Converts a local date + time to UTC.
    /// Handles DST ambiguity by using standard time offset.
    /// Handles DST gaps (spring-forward invalid times) by advancing to the first valid post-gap time.
    /// </summary>
    public DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        var localDt = new DateTime(date.Year, date.Month, date.Day,
            time.Hour, time.Minute, time.Second, DateTimeKind.Unspecified);

        // If the local time falls in a DST gap (invalid time), advance past the gap.
        if (_tz.IsInvalidTime(localDt))
        {
            // Find the DST adjustment rule and advance by the DST delta
            var adjustment = GetAdjustmentForDate(localDt);
            if (adjustment != null)
                localDt = localDt.Add(adjustment.DaylightDelta);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDt, _tz));
    }

    private TimeZoneInfo.AdjustmentRule? GetAdjustmentForDate(DateTime localDt)
    {
        var rules = _tz.GetAdjustmentRules();
        foreach (var rule in rules)
        {
            if (rule.DateStart <= localDt.Date && localDt.Date <= rule.DateEnd)
                return rule;
        }
        return null;
    }

    /// <summary>Converts a UTC DateTimeOffset to local DateOnly in the tenant's timezone.</summary>
    public DateOnly ToLocalDate(DateTimeOffset utc)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc.UtcDateTime, _tz));

    /// <summary>Converts a UTC DateTimeOffset to a local DateTime in the tenant's timezone.</summary>
    public DateTime ToLocalDateTime(DateTimeOffset utc)
        => TimeZoneInfo.ConvertTimeFromUtc(utc.UtcDateTime, _tz);
}
