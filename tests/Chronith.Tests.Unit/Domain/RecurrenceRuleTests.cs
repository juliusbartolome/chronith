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
        IReadOnlyList<DayOfWeek>? daysOfWeek = null)
    {
        return RecurrenceRule.Create(
            tenantId: Guid.NewGuid(),
            bookingTypeId: Guid.NewGuid(),
            frequency: frequency,
            interval: interval,
            daysOfWeek: daysOfWeek,
            seriesStart: seriesStart,
            seriesEnd: seriesEnd,
            maxOccurrences: maxOccurrences);
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
}
