using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class TimeSlotConflictRangeTests
{
    private static readonly DateTimeOffset BaseStart = new(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BaseEnd   = new(2026, 3, 15, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetEffectiveRange_WithNoBuffers_ReturnsSameBounds()
    {
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            bufferBeforeMinutes: 0,
            bufferAfterMinutes: 0);

        var (effectiveStart, effectiveEnd) = bt.GetEffectiveRange(BaseStart, BaseEnd);

        effectiveStart.Should().Be(BaseStart);
        effectiveEnd.Should().Be(BaseEnd);
    }

    [Fact]
    public void GetEffectiveRange_WithBufferBefore_Only_ExpandsStartOnly()
    {
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            bufferBeforeMinutes: 15,
            bufferAfterMinutes: 0);

        var (effectiveStart, effectiveEnd) = bt.GetEffectiveRange(BaseStart, BaseEnd);

        effectiveStart.Should().Be(BaseStart.AddMinutes(-15));
        effectiveEnd.Should().Be(BaseEnd);
    }

    [Fact]
    public void GetEffectiveRange_WithBufferAfter_Only_ExpandsEndOnly()
    {
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            bufferBeforeMinutes: 0,
            bufferAfterMinutes: 10);

        var (effectiveStart, effectiveEnd) = bt.GetEffectiveRange(BaseStart, BaseEnd);

        effectiveStart.Should().Be(BaseStart);
        effectiveEnd.Should().Be(BaseEnd.AddMinutes(10));
    }

    [Fact]
    public void GetEffectiveRange_WithBothBuffers_ExpandsBothEnds()
    {
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            bufferBeforeMinutes: 15,
            bufferAfterMinutes: 10);

        var (effectiveStart, effectiveEnd) = bt.GetEffectiveRange(BaseStart, BaseEnd);

        effectiveStart.Should().Be(BaseStart.AddMinutes(-15));
        effectiveEnd.Should().Be(BaseEnd.AddMinutes(10));
    }
}
