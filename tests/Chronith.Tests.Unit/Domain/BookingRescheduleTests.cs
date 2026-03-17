using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class BookingRescheduleTests
{
    [Theory]
    [InlineData(BookingStatus.PendingPayment)]
    [InlineData(BookingStatus.PendingVerification)]
    [InlineData(BookingStatus.Confirmed)]
    public void Reschedule_FromValidStatus_UpdatesStartAndEnd(BookingStatus status)
    {
        var booking = new BookingBuilder().InStatus(status).Build();
        var newStart = DateTimeOffset.UtcNow.AddDays(7);
        var newEnd = newStart.AddHours(1);

        booking.Reschedule(newStart, newEnd, "admin-1", "TenantAdmin");

        booking.Start.Should().Be(newStart);
        booking.End.Should().Be(newEnd);
    }

    [Fact]
    public void Reschedule_WhenCancelled_Throws()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Cancelled).Build();
        var act = () => booking.Reschedule(
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            "admin-1", "TenantAdmin");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Reschedule_PreservesStatus()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Confirmed).Build();
        var newStart = DateTimeOffset.UtcNow.AddDays(7);

        booking.Reschedule(newStart, newStart.AddHours(1), "admin-1", "TenantAdmin");

        booking.Status.Should().Be(BookingStatus.Confirmed);
    }
}
