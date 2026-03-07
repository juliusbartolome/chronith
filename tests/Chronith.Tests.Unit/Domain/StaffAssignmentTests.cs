using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class StaffAssignmentTests
{
    [Fact]
    public void Booking_StaffMemberIdIsNull_ByDefault()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Confirmed).Build();
        booking.StaffMemberId.Should().BeNull();
    }

    [Fact]
    public void Booking_AssignStaff_SetsStaffMemberId()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Confirmed).Build();
        var staffId = Guid.NewGuid();

        booking.AssignStaff(staffId, "admin-1", "TenantAdmin");

        booking.StaffMemberId.Should().Be(staffId);
    }

    [Fact]
    public void Booking_UnassignStaff_ClearsStaffMemberId()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Confirmed).Build();
        booking.AssignStaff(Guid.NewGuid(), "admin-1", "TenantAdmin");

        booking.UnassignStaff("admin-1", "TenantAdmin");

        booking.StaffMemberId.Should().BeNull();
    }

    [Fact]
    public void Booking_AssignStaff_WhenCancelled_Throws()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Cancelled).Build();

        var act = () => booking.AssignStaff(Guid.NewGuid(), "admin-1", "TenantAdmin");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Theory]
    [InlineData(BookingStatus.PendingPayment)]
    [InlineData(BookingStatus.PendingVerification)]
    [InlineData(BookingStatus.Confirmed)]
    public void Booking_AssignStaff_FromNonCancelledStatus_Succeeds(BookingStatus status)
    {
        var booking = new BookingBuilder().InStatus(status).Build();
        var staffId = Guid.NewGuid();

        booking.AssignStaff(staffId, "admin-1", "TenantAdmin");

        booking.StaffMemberId.Should().Be(staffId);
    }
}
