using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class RecurrenceRuleTests
{
    private static RecurrenceRule CreateRule(
        RecurrenceFrequency frequency,
        int interval,
        DateOnly seriesStart,
        DateOnly? seriesEnd = null,
        int? maxOccurrences = null,
        IReadOnlyList<DayOfWeek>? daysOfWeek = null,
        Guid? customerId = null,
        Guid? staffMemberId = null,
        TimeOnly? startTime = null,
        TimeSpan? duration = null)
    {
        return RecurrenceRule.Create(
            tenantId: Guid.NewGuid(),
            bookingTypeId: Guid.NewGuid(),
            customerId: customerId ?? Guid.NewGuid(),
            staffMemberId: staffMemberId,
            frequency: frequency,
            interval: interval,
            daysOfWeek: daysOfWeek,
            startTime: startTime ?? new TimeOnly(9, 0),
            duration: duration ?? TimeSpan.FromHours(1),
            seriesStart: seriesStart,
            seriesEnd: seriesEnd,
            maxOccurrences: maxOccurrences);
    }

    [Fact]
    public void Create_SetsCustomerIdAndStartTimeAndDuration()
    {
        var customerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var startTime = new TimeOnly(10, 30);
        var duration = TimeSpan.FromMinutes(45);

        var rule = RecurrenceRule.Create(
            tenantId: Guid.NewGuid(),
            bookingTypeId: Guid.NewGuid(),
            customerId: customerId,
            staffMemberId: staffId,
            frequency: RecurrenceFrequency.Daily,
            interval: 1,
            daysOfWeek: null,
            startTime: startTime,
            duration: duration,
            seriesStart: new DateOnly(2026, 1, 1),
            seriesEnd: null,
            maxOccurrences: 5);

        rule.CustomerId.Should().Be(customerId);
        rule.StaffMemberId.Should().Be(staffId);
        rule.StartTime.Should().Be(startTime);
        rule.Duration.Should().Be(duration);
        rule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_StaffMemberIdIsOptional()
    {
        var rule = RecurrenceRule.Create(
            tenantId: Guid.NewGuid(),
            bookingTypeId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            staffMemberId: null,
            frequency: RecurrenceFrequency.Weekly,
            interval: 1,
            daysOfWeek: null,
            startTime: new TimeOnly(9, 0),
            duration: TimeSpan.FromHours(1),
            seriesStart: new DateOnly(2026, 1, 5),
            seriesEnd: null,
            maxOccurrences: 10);

        rule.StaffMemberId.Should().BeNull();
        rule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SoftDelete_SetsIsActiveToFalse()
    {
        var rule = CreateRule(RecurrenceFrequency.Daily, 1, new DateOnly(2026, 1, 1));

        rule.SoftDelete();

        rule.IsActive.Should().BeFalse();
        rule.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Daily_Every1Day_Returns7OccurrencesInWeekWindow()
    {
        // Arrange: daily every 1 day, starting Mon 2026-01-05
        var rule = CreateRule(
            RecurrenceFrequency.Daily,
            interval: 1,
            seriesStart: new DateOnly(2026, 1, 5));

        // Act: query the full week Mon–Sun
        var from = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 11);
        var occurrences = rule.ComputeOccurrences(from, to);

        // Assert
        occurrences.Should().HaveCount(7);
        occurrences.First().Should().Be(new DateOnly(2026, 1, 5));
        occurrences.Last().Should().Be(new DateOnly(2026, 1, 11));
    }

    [Fact]
    public void Daily_Every2Days_CorrectSkipPattern()
    {
        // Arrange: daily every 2 days
        var rule = CreateRule(
            RecurrenceFrequency.Daily,
            interval: 2,
            seriesStart: new DateOnly(2026, 1, 1));

        // Act
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 10);
        var occurrences = rule.ComputeOccurrences(from, to);

        // Assert: Jan 1, 3, 5, 7, 9
        occurrences.Should().BeEquivalentTo(new[]
        {
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 3),
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 7),
            new DateOnly(2026, 1, 9),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Weekly_MonAndWed_OnlyThoseDaysAppear()
    {
        // Arrange: weekly every 1 week, Mon + Wed
        var rule = CreateRule(
            RecurrenceFrequency.Weekly,
            interval: 1,
            seriesStart: new DateOnly(2026, 1, 5), // Monday
            daysOfWeek: new[] { DayOfWeek.Monday, DayOfWeek.Wednesday });

        // Act: two weeks
        var from = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 18);
        var occurrences = rule.ComputeOccurrences(from, to);

        // Assert: Mon 5, Wed 7, Mon 12, Wed 14
        occurrences.Should().BeEquivalentTo(new[]
        {
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 7),
            new DateOnly(2026, 1, 12),
            new DateOnly(2026, 1, 14),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Weekly_Every2Weeks_Biweekly()
    {
        // Arrange: every 2 weeks on Monday
        var rule = CreateRule(
            RecurrenceFrequency.Weekly,
            interval: 2,
            seriesStart: new DateOnly(2026, 1, 5), // Monday
            daysOfWeek: new[] { DayOfWeek.Monday });

        // Act: 5 weeks window
        var from = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 2, 8);
        var occurrences = rule.ComputeOccurrences(from, to);

        // Assert: Jan 5, Jan 19, Feb 2
        occurrences.Should().BeEquivalentTo(new[]
        {
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 19),
            new DateOnly(2026, 2, 2),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Monthly_SameDay_Jan15_Feb15_Mar15()
    {
        // Arrange: monthly every 1 month
        var rule = CreateRule(
            RecurrenceFrequency.Monthly,
            interval: 1,
            seriesStart: new DateOnly(2026, 1, 15));

        // Act
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 3, 31);
        var occurrences = rule.ComputeOccurrences(from, to);

        // Assert
        occurrences.Should().BeEquivalentTo(new[]
        {
            new DateOnly(2026, 1, 15),
            new DateOnly(2026, 2, 15),
            new DateOnly(2026, 3, 15),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Monthly_31st_ClampsToFeb28()
    {
        // Arrange: monthly on the 31st — 2026 is not a leap year
        var rule = CreateRule(
            RecurrenceFrequency.Monthly,
            interval: 1,
            seriesStart: new DateOnly(2026, 1, 31));

        // Act
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 4, 30);
        var occurrences = rule.ComputeOccurrences(from, to);

        // Assert: Jan 31, Feb 28, Mar 31, Apr 30
        occurrences.Should().BeEquivalentTo(new[]
        {
            new DateOnly(2026, 1, 31),
            new DateOnly(2026, 2, 28),
            new DateOnly(2026, 3, 31),
            new DateOnly(2026, 4, 30),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void MaxOccurrences_5_StopsAfter5()
    {
        // Arrange: daily, max 5 occurrences
        var rule = CreateRule(
            RecurrenceFrequency.Daily,
            interval: 1,
            seriesStart: new DateOnly(2026, 1, 1),
            maxOccurrences: 5);

        // Act: large window
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 12, 31);
        var occurrences = rule.ComputeOccurrences(from, to);

        // Assert: only 5 dates
        occurrences.Should().HaveCount(5);
        occurrences.Last().Should().Be(new DateOnly(2026, 1, 5));
    }

    [Fact]
    public void SeriesEnd_StopsAtEndDate()
    {
        // Arrange: daily, series ends Jan 3
        var rule = CreateRule(
            RecurrenceFrequency.Daily,
            interval: 1,
            seriesStart: new DateOnly(2026, 1, 1),
            seriesEnd: new DateOnly(2026, 1, 3));

        // Act: large window
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 12, 31);
        var occurrences = rule.ComputeOccurrences(from, to);

        // Assert: only 3 dates
        occurrences.Should().HaveCount(3);
        occurrences.Should().BeEquivalentTo(new[]
        {
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 2),
            new DateOnly(2026, 1, 3),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void FromToWindowing_OnlyReturnsDatesInRange()
    {
        // Arrange: daily starting Jan 1
        var rule = CreateRule(
            RecurrenceFrequency.Daily,
            interval: 1,
            seriesStart: new DateOnly(2026, 1, 1));

        // Act: query only Jan 5–Jan 7 (series starts before 'from')
        var from = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 7);
        var occurrences = rule.ComputeOccurrences(from, to);

        // Assert: only 3 dates in the window
        occurrences.Should().HaveCount(3);
        occurrences.Should().BeEquivalentTo(new[]
        {
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 6),
            new DateOnly(2026, 1, 7),
        }, options => options.WithStrictOrdering());
        occurrences.All(d => d >= from && d <= to).Should().BeTrue();
    }

    [Fact]
    public void Create_IntervalLessThan1_Throws()
    {
        var act = () => CreateRule(
            RecurrenceFrequency.Daily,
            interval: 0,
            seriesStart: new DateOnly(2026, 1, 1));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("interval");
    }

    [Fact]
    public void Create_SeriesEndBeforeSeriesStart_Throws()
    {
        var act = () => CreateRule(
            RecurrenceFrequency.Daily,
            interval: 1,
            seriesStart: new DateOnly(2026, 6, 1),
            seriesEnd: new DateOnly(2026, 1, 1));

        act.Should().Throw<ArgumentException>()
            .WithParameterName("seriesEnd");
    }

    [Fact]
    public void Weekly_MonWed_MaxOccurrences3_StopsMidWeek()
    {
        // Arrange: weekly Mon+Wed, max 3 occurrences
        var rule = CreateRule(
            RecurrenceFrequency.Weekly,
            interval: 1,
            seriesStart: new DateOnly(2026, 1, 5), // Monday
            maxOccurrences: 3,
            daysOfWeek: new[] { DayOfWeek.Monday, DayOfWeek.Wednesday });

        // Act
        var from = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 2, 28);
        var occurrences = rule.ComputeOccurrences(from, to);

        // Assert: Mon 5, Wed 7, Mon 12 — stops after 3
        occurrences.Should().HaveCount(3);
        occurrences.Should().BeEquivalentTo(new[]
        {
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 7),
            new DateOnly(2026, 1, 12),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Weekly_EmptyDaysOfWeek_BehavesSameAsNull()
    {
        // Arrange: weekly with empty DaysOfWeek list (should behave like null — step by week)
        var rule = CreateRule(
            RecurrenceFrequency.Weekly,
            interval: 1,
            seriesStart: new DateOnly(2026, 1, 5), // Monday
            daysOfWeek: Array.Empty<DayOfWeek>());

        // Act
        var from = new DateOnly(2026, 1, 5);
        var to = new DateOnly(2026, 1, 25);
        var occurrences = rule.ComputeOccurrences(from, to);

        // Assert: Jan 5, 12, 19 (step by 7 days each)
        occurrences.Should().BeEquivalentTo(new[]
        {
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 12),
            new DateOnly(2026, 1, 19),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void MaxOccurrences_WithWindowedFrom_CountsFromSeriesStart()
    {
        // Arrange: daily, max 5, starts Jan 1
        var rule = CreateRule(
            RecurrenceFrequency.Daily,
            interval: 1,
            seriesStart: new DateOnly(2026, 1, 1),
            maxOccurrences: 5);

        // Act: query from Jan 4 — occurrences 1-3 (Jan 1-3) are before window
        var from = new DateOnly(2026, 1, 4);
        var to = new DateOnly(2026, 12, 31);
        var occurrences = rule.ComputeOccurrences(from, to);

        // Assert: only Jan 4, Jan 5 in window (occurrences 4 and 5 of 5 total)
        occurrences.Should().HaveCount(2);
        occurrences.Should().BeEquivalentTo(new[]
        {
            new DateOnly(2026, 1, 4),
            new DateOnly(2026, 1, 5),
        }, options => options.WithStrictOrdering());
    }
}
