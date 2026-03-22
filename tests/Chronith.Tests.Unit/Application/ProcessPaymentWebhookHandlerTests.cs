using Chronith.Application.Commands.Bookings;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Notifications;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using MediatR;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Chronith.Tests.Unit.Application;

public sealed class ProcessPaymentWebhookHandlerTests
{
    private readonly ITenantPaymentProviderResolver _resolver;
    private readonly IBookingRepository _bookingRepo;
    private readonly IBookingTypeRepository _bookingTypeRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly ProcessPaymentWebhookHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public ProcessPaymentWebhookHandlerTests()
    {
        _resolver = Substitute.For<ITenantPaymentProviderResolver>();
        _bookingRepo = Substitute.For<IBookingRepository>();
        _bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _publisher = Substitute.For<IPublisher>();
        _handler = new ProcessPaymentWebhookHandler(
            _resolver, _bookingRepo, _bookingTypeRepo, _unitOfWork, _publisher);
    }

    [Fact]
    public async Task Handle_WhenResolverReturnsNull_ThrowsUnauthorizedException()
    {
        _resolver.ResolveAsync(TenantId, "PayMongo", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var cmd = new ProcessPaymentWebhookCommand
        {
            TenantId = TenantId,
            ProviderName = "PayMongo",
            RawBody = "{}",
            Headers = new Dictionary<string, string>()
        };

        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ThrowsUnauthorizedException()
    {
        var provider = Substitute.For<IPaymentProvider>();
        provider.ValidateWebhook(Arg.Any<WebhookValidationContext>()).Returns(false);

        _resolver.ResolveAsync(TenantId, "PayMongo", Arg.Any<CancellationToken>())
            .Returns(provider);

        var cmd = new ProcessPaymentWebhookCommand
        {
            TenantId = TenantId,
            ProviderName = "PayMongo",
            RawBody = "{}",
            Headers = new Dictionary<string, string>()
        };

        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_WhenEventIsNotSuccess_DoesNotUpdateBooking()
    {
        var provider = Substitute.For<IPaymentProvider>();
        provider.ValidateWebhook(Arg.Any<WebhookValidationContext>()).Returns(true);
        provider.ParseWebhookPayload(Arg.Any<string>())
            .Returns(new WebhookPaymentEvent("ref-123", PaymentEventType.Failed));

        _resolver.ResolveAsync(TenantId, "PayMongo", Arg.Any<CancellationToken>())
            .Returns(provider);

        var cmd = new ProcessPaymentWebhookCommand
        {
            TenantId = TenantId,
            ProviderName = "PayMongo",
            RawBody = "{}",
            Headers = new Dictionary<string, string>()
        };

        await _handler.Handle(cmd, CancellationToken.None);

        await _bookingRepo.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }
}
