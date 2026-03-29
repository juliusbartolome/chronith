using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class BookingProofTests
{
    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public void SubmitProofOfPayment_FromPendingPayment_TransitionsToPendingVerification()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

        booking.SubmitProofOfPayment(
            "https://storage.example.com/proof.jpg",
            "proof.jpg",
            "Paid via GCash",
            "customer-1",
            "customer");

        booking.Status.Should().Be(BookingStatus.PendingVerification);
    }

    [Fact]
    public void SubmitProofOfPayment_SetsProofFields()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

        booking.SubmitProofOfPayment(
            "https://storage.example.com/proof.jpg",
            "proof.jpg",
            "Paid via GCash",
            "customer-1",
            "customer");

        booking.ProofOfPaymentUrl.Should().Be("https://storage.example.com/proof.jpg");
        booking.ProofOfPaymentFileName.Should().Be("proof.jpg");
        booking.PaymentNote.Should().Be("Paid via GCash");
    }

    [Fact]
    public void SubmitProofOfPayment_AddsStatusChange()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

        booking.SubmitProofOfPayment(
            "https://storage.example.com/proof.jpg",
            "proof.jpg",
            "Paid via GCash",
            "customer-1",
            "customer");

        booking.StatusChanges.Should().ContainSingle();
        var change = booking.StatusChanges[0];
        change.FromStatus.Should().Be(BookingStatus.PendingPayment);
        change.ToStatus.Should().Be(BookingStatus.PendingVerification);
        change.ChangedById.Should().Be("customer-1");
        change.ChangedByRole.Should().Be("customer");
    }

    [Fact]
    public void SubmitProofOfPayment_WithNullProof_StillTransitions()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

        booking.SubmitProofOfPayment(null, null, null, "customer-1", "customer");

        booking.Status.Should().Be(BookingStatus.PendingVerification);
        booking.ProofOfPaymentUrl.Should().BeNull();
        booking.ProofOfPaymentFileName.Should().BeNull();
        booking.PaymentNote.Should().BeNull();
    }

    // ── Invalid state transitions ────────────────────────────────────────────

    [Fact]
    public void SubmitProofOfPayment_FromConfirmed_Throws()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Confirmed).Build();

        var act = () => booking.SubmitProofOfPayment(
            "https://storage.example.com/proof.jpg", "proof.jpg", "note", "customer-1", "customer");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void SubmitProofOfPayment_FromCancelled_Throws()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.Cancelled).Build();

        var act = () => booking.SubmitProofOfPayment(
            "https://storage.example.com/proof.jpg", "proof.jpg", "note", "customer-1", "customer");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void SubmitProofOfPayment_FromPendingVerification_Throws()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PendingVerification).Build();

        var act = () => booking.SubmitProofOfPayment(
            "https://storage.example.com/proof.jpg", "proof.jpg", "note", "customer-1", "customer");

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void SubmitProofOfPayment_FromPaymentFailed_Throws()
    {
        var booking = new BookingBuilder().InStatus(BookingStatus.PaymentFailed).Build();

        var act = () => booking.SubmitProofOfPayment(
            "https://storage.example.com/proof.jpg", "proof.jpg", "note", "customer-1", "customer");

        act.Should().Throw<InvalidStateTransitionException>();
    }
}
