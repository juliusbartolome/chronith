using Chronith.Application.Commands.Bookings;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Notifications;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<ProcessPaymentWebhookHandler> _logger;
    private readonly ProcessPaymentWebhookHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public ProcessPaymentWebhookHandlerTests()
    {
        _resolver = Substitute.For<ITenantPaymentProviderResolver>();
        _bookingRepo = Substitute.For<IBookingRepository>();
        _bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _publisher = Substitute.For<IPublisher>();
        _logger = Substitute.For<ILogger<ProcessPaymentWebhookHandler>>();
        _handler = new ProcessPaymentWebhookHandler(
            _resolver, _bookingRepo, _bookingTypeRepo, _unitOfWork, _publisher, _logger);
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
    public async Task Handle_WhenEventIsSuccess_TransitionsBookingToConfirmed()
    {
        var provider = Substitute.For<IPaymentProvider>();
        provider.ValidateWebhook(Arg.Any<WebhookValidationContext>()).Returns(true);
        provider.ParseWebhookPayload(Arg.Any<string>())
            .Returns(new WebhookPaymentEvent("ref-123", PaymentEventType.Success));

        _resolver.ResolveAsync(TenantId, "PayMongo", Arg.Any<CancellationToken>())
            .Returns(provider);

        var booking = new BookingBuilder()
            .InStatus(BookingStatus.PendingPayment)
            .WithPaymentReference("ref-123")
            .Build();

        _bookingRepo.GetByPaymentReferenceAsync(TenantId, "ref-123", Arg.Any<CancellationToken>())
            .Returns(booking);

        _bookingTypeRepo.GetByIdAsync(booking.BookingTypeId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        var cmd = new ProcessPaymentWebhookCommand
        {
            TenantId = TenantId,
            ProviderName = "PayMongo",
            RawBody = "{}",
            Headers = new Dictionary<string, string>()
        };

        await _handler.Handle(cmd, CancellationToken.None);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        await _bookingRepo.Received(1).UpdateAsync(booking, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publisher.Received(1).Publish(
            Arg.Is<BookingStatusChangedNotification>(n =>
                n.ToStatus == BookingStatus.Confirmed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEventIsFailed_TransitionsBookingToPaymentFailed()
    {
        var provider = Substitute.For<IPaymentProvider>();
        provider.ValidateWebhook(Arg.Any<WebhookValidationContext>()).Returns(true);
        provider.ParseWebhookPayload(Arg.Any<string>())
            .Returns(new WebhookPaymentEvent("ref-123", PaymentEventType.Failed));

        _resolver.ResolveAsync(TenantId, "PayMongo", Arg.Any<CancellationToken>())
            .Returns(provider);

        var booking = new BookingBuilder()
            .InStatus(BookingStatus.PendingPayment)
            .WithPaymentReference("ref-123")
            .Build();

        _bookingRepo.GetByPaymentReferenceAsync(TenantId, "ref-123", Arg.Any<CancellationToken>())
            .Returns(booking);

        _bookingTypeRepo.GetByIdAsync(booking.BookingTypeId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        var cmd = new ProcessPaymentWebhookCommand
        {
            TenantId = TenantId,
            ProviderName = "PayMongo",
            RawBody = "{}",
            Headers = new Dictionary<string, string>()
        };

        await _handler.Handle(cmd, CancellationToken.None);

        booking.Status.Should().Be(BookingStatus.PaymentFailed);
        await _bookingRepo.Received(1).UpdateAsync(booking, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publisher.Received(1).Publish(
            Arg.Is<BookingStatusChangedNotification>(n =>
                n.ToStatus == BookingStatus.PaymentFailed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEventIsFailed_AndBookingNotFound_ThrowsNotFoundException()
    {
        var provider = Substitute.For<IPaymentProvider>();
        provider.ValidateWebhook(Arg.Any<WebhookValidationContext>()).Returns(true);
        provider.ParseWebhookPayload(Arg.Any<string>())
            .Returns(new WebhookPaymentEvent("ref-missing", PaymentEventType.Failed));

        _resolver.ResolveAsync(TenantId, "PayMongo", Arg.Any<CancellationToken>())
            .Returns(provider);

        _bookingRepo.GetByPaymentReferenceAsync(TenantId, "ref-missing", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var cmd = new ProcessPaymentWebhookCommand
        {
            TenantId = TenantId,
            ProviderName = "PayMongo",
            RawBody = "{}",
            Headers = new Dictionary<string, string>()
        };

        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
