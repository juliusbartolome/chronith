using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Public;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class GetPublicBookingStatusQueryHandlerTests
{
    private readonly IBookingRepository _repo = Substitute.For<IBookingRepository>();

    private static Booking CreateBooking(BookingStatus status, string? checkoutUrl = null)
    {
        var booking = Booking.Create(
            tenantId: Guid.NewGuid(),
            bookingTypeId: Guid.NewGuid(),
            start: DateTimeOffset.UtcNow.AddDays(1),
            end: DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            customerId: "cust_001",
            customerEmail: "customer@example.com",
            amountInCentavos: status == BookingStatus.PendingPayment ? 50000L : 0L,
            currency: "PHP");

        if (checkoutUrl is not null)
            booking.SetCheckoutUrl(checkoutUrl);

        // Force status to desired value by executing transitions
        if (status == BookingStatus.PendingVerification && booking.AmountInCentavos > 0)
            booking.Pay("system", "system");
        else if (status == BookingStatus.Confirmed)
            booking.Confirm("system", "system");
        else if (status == BookingStatus.Cancelled)
            booking.Cancel("system", "system");

        return booking;
    }

    [Fact]
    public async Task Handle_ReturnsDtoWithCheckoutUrl_WhenStatusIsPendingPayment()
    {
        var tenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var checkoutUrl = "https://checkout.paymongo.com/session_abc";

        var booking = Booking.Create(
            tenantId: tenantId,
            bookingTypeId: Guid.NewGuid(),
            start: DateTimeOffset.UtcNow.AddDays(1),
            end: DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            customerId: "cust_001",
            customerEmail: "customer@example.com",
            amountInCentavos: 50000L,
            currency: "PHP",
            paymentReference: "cs_live_abc123");
        booking.SetCheckoutUrl(checkoutUrl);

        _repo.GetPublicByIdAsync(tenantId, bookingId, Arg.Any<CancellationToken>())
             .Returns(booking);

        var handler = new GetPublicBookingStatusQueryHandler(_repo);
        var result = await handler.Handle(
            new GetPublicBookingStatusQuery(tenantId, bookingId), CancellationToken.None);

        result.Status.Should().Be(BookingStatus.PendingPayment);
        result.CheckoutUrl.Should().Be(checkoutUrl);
        result.PaymentReference.Should().Be("cs_live_abc123");
        result.AmountInCentavos.Should().Be(50000L);
        result.Currency.Should().Be("PHP");
    }

    [Fact]
    public async Task Handle_NullsOutCheckoutUrl_WhenStatusIsNotPendingPayment()
    {
        var tenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        // Free booking → starts at PendingVerification
        var booking = Booking.Create(
            tenantId: tenantId,
            bookingTypeId: Guid.NewGuid(),
            start: DateTimeOffset.UtcNow.AddDays(1),
            end: DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            customerId: "cust_001",
            customerEmail: "customer@example.com",
            amountInCentavos: 0L,
            currency: "PHP");
        booking.SetCheckoutUrl("https://should-not-be-returned.example.com");

        _repo.GetPublicByIdAsync(tenantId, bookingId, Arg.Any<CancellationToken>())
             .Returns(booking);

        var handler = new GetPublicBookingStatusQueryHandler(_repo);
        var result = await handler.Handle(
            new GetPublicBookingStatusQuery(tenantId, bookingId), CancellationToken.None);

        result.Status.Should().Be(BookingStatus.PendingVerification);
        result.CheckoutUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenBookingNotFound()
    {
        var tenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        Booking? notFound = null;
        _repo.GetPublicByIdAsync(tenantId, bookingId, Arg.Any<CancellationToken>())
             .Returns(notFound);

        var handler = new GetPublicBookingStatusQueryHandler(_repo);
        var act = () => handler.Handle(
            new GetPublicBookingStatusQuery(tenantId, bookingId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
