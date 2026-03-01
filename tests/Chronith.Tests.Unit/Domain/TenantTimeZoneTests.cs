using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class TenantTimeZoneTests
{
    [Fact]
    public void ToUtc_UTC_NoOffset_IsIdentity()
    {
        var tz = new TenantTimeZone("UTC");
        var date = new DateOnly(2026, 3, 15);
        var time = new TimeOnly(10, 30, 0);

        var result = tz.ToUtc(date, time);

        result.UtcDateTime.Should().Be(new DateTime(2026, 3, 15, 10, 30, 0, DateTimeKind.Utc));
        result.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ToUtc_EasternTime_ConvertsCorrectly()
    {
        // Winter: America/New_York is UTC-5 (EST)
        // 2026-01-15 10:00 EST = 2026-01-15 15:00 UTC
        var tz = new TenantTimeZone("America/New_York");
        var date = new DateOnly(2026, 1, 15);
        var time = new TimeOnly(10, 0, 0);

        var result = tz.ToUtc(date, time);

        result.UtcDateTime.Should().Be(new DateTime(2026, 1, 15, 15, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToLocalDate_UTC_ReturnsCorrectDate()
    {
        var tz = new TenantTimeZone("UTC");
        var utc = new DateTimeOffset(2026, 3, 15, 14, 30, 0, TimeSpan.Zero);

        var localDate = tz.ToLocalDate(utc);

        localDate.Should().Be(new DateOnly(2026, 3, 15));
    }

    [Fact]
    public void ToLocalDate_NegativeOffset_MightReturnPreviousDay()
    {
        // America/New_York UTC-5 in winter
        // 2026-01-15 02:00 UTC = 2026-01-14 21:00 EST
        var tz = new TenantTimeZone("America/New_York");
        var utc = new DateTimeOffset(2026, 1, 15, 2, 0, 0, TimeSpan.Zero);

        var localDate = tz.ToLocalDate(utc);

        // Should be previous day in New York
        localDate.Should().Be(new DateOnly(2026, 1, 14));
    }

    [Fact]
    public void ToLocalDateTime_ConvertsCorrectly()
    {
        // America/New_York UTC-5 in winter
        // 2026-01-15 15:00 UTC = 2026-01-15 10:00 EST
        var tz = new TenantTimeZone("America/New_York");
        var utc = new DateTimeOffset(2026, 1, 15, 15, 0, 0, TimeSpan.Zero);

        var localDt = tz.ToLocalDateTime(utc);

        localDt.Should().Be(new DateTime(2026, 1, 15, 10, 0, 0));
    }
}
