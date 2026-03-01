using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class BookingStateMachineTests
{
    // ── Pay transitions ──────────────────────────────────────────────────────

    [Fact]
    public void Pay_From_PendingPayment_Transitions_To_PendingVerification()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

        booking.Pay("user-1", "admin");

        booking.Status.Should().Be(BookingStatus.PendingVerification);
    }

    [Fact]
    public void Pay_From_PendingVerification_Throws_InvalidStateTransitionException()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingVerification).Build();

        var act = () => booking.Pay("user-1", "admin");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Pay_From_Confirmed_Throws_InvalidStateTransitionException()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Confirmed).Build();

        var act = () => booking.Pay("user-1", "admin");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Pay_From_Cancelled_Throws_InvalidStateTransitionException()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Cancelled).Build();

        var act = () => booking.Pay("user-1", "admin");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    // ── Confirm transitions ──────────────────────────────────────────────────

    [Fact]
    public void Confirm_From_PendingVerification_Transitions_To_Confirmed()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingVerification).Build();

        booking.Confirm("user-1", "admin");

        booking.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public void Confirm_From_PendingPayment_Throws_InvalidStateTransitionException()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

        var act = () => booking.Confirm("user-1", "admin");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Confirm_From_Confirmed_Throws_InvalidStateTransitionException()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Confirmed).Build();

        var act = () => booking.Confirm("user-1", "admin");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Confirm_From_Cancelled_Throws_InvalidStateTransitionException()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Cancelled).Build();

        var act = () => booking.Confirm("user-1", "admin");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    // ── Cancel transitions ───────────────────────────────────────────────────

    [Fact]
    public void Cancel_From_PendingPayment_Transitions_To_Cancelled()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

        booking.Cancel("user-1", "admin");

        booking.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public void Cancel_From_PendingVerification_Transitions_To_Cancelled()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingVerification).Build();

        booking.Cancel("user-1", "admin");

        booking.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public void Cancel_From_Confirmed_Transitions_To_Cancelled()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Confirmed).Build();

        booking.Cancel("user-1", "admin");

        booking.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public void Cancel_From_Cancelled_Throws_InvalidStateTransitionException()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Cancelled).Build();

        var act = () => booking.Cancel("user-1", "admin");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    // ── Status change log ────────────────────────────────────────────────────

    [Fact]
    public void Pay_Appends_StatusChange_With_Correct_FromAndTo()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

        booking.Pay("user-1", "admin");

        booking.StatusChanges.Should().ContainSingle();
        var change = booking.StatusChanges[0];
        change.FromStatus.Should().Be(BookingStatus.PendingPayment);
        change.ToStatus.Should().Be(BookingStatus.PendingVerification);
    }

    [Fact]
    public void Pay_Appends_StatusChange_With_ChangedById()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

        booking.Pay("user-42", "customer");

        booking.StatusChanges.Should().ContainSingle();
        booking.StatusChanges[0].ChangedById.Should().Be("user-42");
    }

    [Fact]
    public void StatusChanges_AreAppendOnly_AfterMultipleTransitions()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

        booking.Pay("user-1", "admin");
        booking.Confirm("user-2", "admin");
        booking.Cancel("user-3", "admin");

        booking.StatusChanges.Should().HaveCount(3);
        booking.StatusChanges[0].FromStatus.Should().Be(BookingStatus.PendingPayment);
        booking.StatusChanges[0].ToStatus.Should().Be(BookingStatus.PendingVerification);
        booking.StatusChanges[1].FromStatus.Should().Be(BookingStatus.PendingVerification);
        booking.StatusChanges[1].ToStatus.Should().Be(BookingStatus.Confirmed);
        booking.StatusChanges[2].FromStatus.Should().Be(BookingStatus.Confirmed);
        booking.StatusChanges[2].ToStatus.Should().Be(BookingStatus.Cancelled);
    }
}
