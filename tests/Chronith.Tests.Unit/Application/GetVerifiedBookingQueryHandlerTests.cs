using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Public;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class GetVerifiedBookingQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BookingId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ValidBooking_ReturnsDto()
    {
        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .InStatus(BookingStatus.PendingPayment)
            .WithAmount(50_000)
            .Build();

        var repo = Substitute.For<IBookingRepository>();
        repo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        var handler = new GetVerifiedBookingQueryHandler(repo);

        var result = await handler.Handle(
            new GetVerifiedBookingQuery(TenantId, BookingId), CancellationToken.None);

        result.Id.Should().Be(BookingId);
        result.ReferenceId.Should().Be(BookingId.ToString("N"));
        result.Status.Should().Be(BookingStatus.PendingPayment);
        result.AmountInCentavos.Should().Be(50_000);
    }

    [Fact]
    public async Task Handle_BookingNotFound_ThrowsNotFoundException()
    {
        var repo = Substitute.For<IBookingRepository>();
        repo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(default(Booking));

        var handler = new GetVerifiedBookingQueryHandler(repo);

        var act = () => handler.Handle(
            new GetVerifiedBookingQuery(TenantId, BookingId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_PendingPayment_ExposesCheckoutUrl()
    {
        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .InStatus(BookingStatus.PendingPayment)
            .WithCheckoutUrl("https://checkout.paymongo.com/cs_123")
            .Build();

        var repo = Substitute.For<IBookingRepository>();
        repo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        var handler = new GetVerifiedBookingQueryHandler(repo);
        var result = await handler.Handle(
            new GetVerifiedBookingQuery(TenantId, BookingId), CancellationToken.None);

        result.CheckoutUrl.Should().Be("https://checkout.paymongo.com/cs_123");
    }

    [Fact]
    public async Task Handle_Confirmed_HidesCheckoutUrl()
    {
        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .InStatus(BookingStatus.Confirmed)
            .WithCheckoutUrl("https://checkout.paymongo.com/cs_123")
            .Build();

        var repo = Substitute.For<IBookingRepository>();
        repo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        var handler = new GetVerifiedBookingQueryHandler(repo);
        var result = await handler.Handle(
            new GetVerifiedBookingQuery(TenantId, BookingId), CancellationToken.None);

        result.CheckoutUrl.Should().BeNull();
    }
}
