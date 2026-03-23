using Chronith.Application.Commands.Public;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class CreatePublicCheckoutCommandHandlerTests
{
    private static readonly Guid BookingId = Guid.NewGuid();
    private static readonly Tenant TestTenant = Tenant.Create("test-tenant", "Test", "UTC");

    private static (CreatePublicCheckoutHandler Handler, IPaymentProvider Provider, IBookingRepository BookingRepo)
        Build(Booking? booking = null, IPaymentProvider? provider = null)
    {
        var bookingRepo = Substitute.For<IBookingRepository>();
        var resolvedBooking = booking ?? new BookingBuilder()
            .WithTenantId(TestTenant.Id)
            .WithId(BookingId)
            .InStatus(BookingStatus.PendingPayment)
            .WithAmount(50_000)
            .Build();
        bookingRepo.GetPublicByIdAsync(TestTenant.Id, BookingId, Arg.Any<CancellationToken>())
            .Returns(resolvedBooking);

        var tenantRepo = Substitute.For<ITenantRepository>();
        tenantRepo.GetBySlugAsync("test-tenant", Arg.Any<CancellationToken>())
            .Returns(TestTenant);

        var mockProvider = provider ?? Substitute.For<IPaymentProvider>();
        mockProvider.CreateCheckoutSessionAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateCheckoutResult("https://checkout.paymongo.com/cs_123", "cs_123"));

        var resolver = Substitute.For<ITenantPaymentProviderResolver>();
        resolver.ResolveAsync(TestTenant.Id, "PayMongo", Arg.Any<CancellationToken>())
            .Returns(mockProvider);

        var signer = Substitute.For<IBookingUrlSigner>();
        signer.GenerateSignedUrl(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns("https://test.com/pay/success?sig=abc");

        var pageOptions = Options.Create(new PaymentPageOptions { BaseUrl = "https://test.com/pay" });

        var handler = new CreatePublicCheckoutHandler(
            bookingRepo, tenantRepo, resolver, signer, pageOptions);

        return (handler, mockProvider, bookingRepo);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsCheckoutUrl()
    {
        var (handler, _, _) = Build();

        var result = await handler.Handle(new CreatePublicCheckoutCommand
        {
            TenantSlug = "test-tenant",
            BookingId = BookingId,
            ProviderName = "PayMongo"
        }, CancellationToken.None);

        result.CheckoutUrl.Should().Be("https://checkout.paymongo.com/cs_123");
    }

    [Fact]
    public async Task Handle_ValidRequest_StoresCheckoutDetailsOnBooking()
    {
        var (handler, _, bookingRepo) = Build();

        await handler.Handle(new CreatePublicCheckoutCommand
        {
            TenantSlug = "test-tenant",
            BookingId = BookingId,
            ProviderName = "PayMongo"
        }, CancellationToken.None);

        await bookingRepo.Received(1).UpdateAsync(
            Arg.Is<Booking>(b => b.CheckoutUrl == "https://checkout.paymongo.com/cs_123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BookingNotPendingPayment_Throws()
    {
        var booking = new BookingBuilder()
            .WithTenantId(TestTenant.Id)
            .WithId(BookingId)
            .InStatus(BookingStatus.Confirmed)
            .Build();
        var (handler, _, _) = Build(booking);

        var act = () => handler.Handle(new CreatePublicCheckoutCommand
        {
            TenantSlug = "test-tenant",
            BookingId = BookingId,
            ProviderName = "PayMongo"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    [Fact]
    public async Task Handle_PassesHmacSignedSuccessUrl()
    {
        var (handler, provider, _) = Build();

        await handler.Handle(new CreatePublicCheckoutCommand
        {
            TenantSlug = "test-tenant",
            BookingId = BookingId,
            ProviderName = "PayMongo"
        }, CancellationToken.None);

        await provider.Received(1).CreateCheckoutSessionAsync(
            Arg.Is<CreateCheckoutRequest>(r =>
                r.SuccessUrl != null && r.SuccessUrl.Contains("sig=")),
            Arg.Any<CancellationToken>());
    }
}
