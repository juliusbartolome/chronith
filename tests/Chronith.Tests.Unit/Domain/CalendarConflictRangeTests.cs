using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class CalendarConflictRangeTests
{
    private static readonly DateTimeOffset BaseStart = new(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BaseEnd   = new(2026, 3, 16, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetEffectiveRange_ReturnsUnchangedBounds()
    {
        var bt = BookingTypeBuilder.BuildCalendar(
            availableDays: [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
                             DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday]);

        var (effectiveStart, effectiveEnd) = bt.GetEffectiveRange(BaseStart, BaseEnd);

        effectiveStart.Should().Be(BaseStart);
        effectiveEnd.Should().Be(BaseEnd);
    }
}
