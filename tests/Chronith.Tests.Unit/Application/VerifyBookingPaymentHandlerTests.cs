using Chronith.Application.Commands.Public;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Notifications;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using FluentValidation.TestHelper;
using MediatR;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class VerifyBookingPaymentHandlerTests
{
    private readonly IBookingUrlSigner _signer = Substitute.For<IBookingUrlSigner>();
    private readonly ITenantRepository _tenantRepo = Substitute.For<ITenantRepository>();
    private readonly IBookingRepository _bookingRepo = Substitute.For<IBookingRepository>();
    private readonly IBookingTypeRepository _bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
    private readonly ITenantPaymentConfigRepository _paymentConfigRepo = Substitute.For<ITenantPaymentConfigRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();

    private static readonly Guid BookingId = Guid.NewGuid();
    private static readonly Guid BookingTypeId = Guid.NewGuid();
    private const string TenantSlug = "test-tenant";

    private static readonly Tenant TestTenant = Tenant.Create(TenantSlug, "Test Tenant", "Asia/Manila");
    private static Guid TenantId => TestTenant.Id;

    private VerifyBookingPaymentCommandHandler CreateHandler()
        => new(_signer, _tenantRepo, _bookingRepo, _bookingTypeRepo, _paymentConfigRepo,
               _unitOfWork, _publisher);

    private static TimeSlotBookingType CreateBookingType()
        => TimeSlotBookingType.Create(
            tenantId: TenantId,
            slug: "manual-type",
            name: "Manual Type",
            capacity: 1,
            paymentMode: PaymentMode.Manual,
            paymentProvider: null,
            durationMinutes: 60,
            bufferBeforeMinutes: 0,
            bufferAfterMinutes: 0,
            availabilityWindows: [],
            priceInCentavos: 50000,
            currency: "PHP");

    private void SetupValidSignerAndTenant()
    {
        _signer.ValidateStaffVerify(BookingId, TenantSlug, Arg.Any<long>(), Arg.Any<string>())
            .Returns(true);

        _tenantRepo.GetBySlugAsync(TenantSlug, Arg.Any<CancellationToken>())
            .Returns(TestTenant);
    }

    private static VerifyBookingPaymentCommand CreateApproveCommand() => new()
    {
        TenantSlug = TenantSlug,
        BookingId = BookingId,
        Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
        Signature = "valid-sig",
        Action = "approve",
        Note = null
    };

    private static VerifyBookingPaymentCommand CreateRejectCommand() => new()
    {
        TenantSlug = TenantSlug,
        BookingId = BookingId,
        Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
        Signature = "valid-sig",
        Action = "reject",
        Note = "Payment proof unclear"
    };

    // ── Test 1: Handler approves booking (PendingVerification → Confirmed) ──

    [Fact]
    public async Task Handle_ApproveAction_ConfirmsBooking()
    {
        SetupValidSignerAndTenant();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingVerification)
            .WithAmount(50000)
            .Build();

        var bookingType = CreateBookingType();

        _bookingRepo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var handler = CreateHandler();
        var command = CreateApproveCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        result.Status.Should().Be(BookingStatus.Confirmed);
        await _bookingRepo.Received(1).UpdatePublicAsync(booking, TenantId, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Test 2: Handler rejects booking (PendingVerification → Cancelled) ──

    [Fact]
    public async Task Handle_RejectAction_CancelsBooking()
    {
        SetupValidSignerAndTenant();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingVerification)
            .WithAmount(50000)
            .Build();

        var bookingType = CreateBookingType();

        _bookingRepo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var handler = CreateHandler();
        var command = CreateRejectCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        booking.Status.Should().Be(BookingStatus.Cancelled);
        result.Status.Should().Be(BookingStatus.Cancelled);
        await _bookingRepo.Received(1).UpdatePublicAsync(booking, TenantId, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Test 3: Handler throws on invalid HMAC ──

    [Fact]
    public async Task Handle_InvalidSignature_ThrowsUnauthorizedException()
    {
        _signer.ValidateStaffVerify(BookingId, TenantSlug, Arg.Any<long>(), Arg.Any<string>())
            .Returns(false);

        var handler = CreateHandler();
        var command = CreateApproveCommand();

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    // ── Test 4: Handler throws on wrong booking status ──

    [Fact]
    public async Task Handle_BookingAlreadyConfirmed_ThrowsInvalidStateTransition()
    {
        SetupValidSignerAndTenant();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.Confirmed)
            .WithAmount(50000)
            .Build();

        _bookingRepo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        var handler = CreateHandler();
        var command = CreateApproveCommand();

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    // ── Test 5: Handler publishes notification on approve ──

    [Fact]
    public async Task Handle_ApproveAction_PublishesNotification()
    {
        SetupValidSignerAndTenant();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingVerification)
            .WithAmount(50000)
            .WithCustomerEmail("test@example.com")
            .Build();

        var bookingType = CreateBookingType();

        _bookingRepo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var handler = CreateHandler();
        var command = CreateApproveCommand();

        await handler.Handle(command, CancellationToken.None);

        await _publisher.Received(1).Publish(
            Arg.Is<BookingStatusChangedNotification>(n =>
                n.BookingId == BookingId &&
                n.TenantId == TenantId &&
                n.BookingTypeSlug == "manual-type" &&
                n.FromStatus == BookingStatus.PendingVerification &&
                n.ToStatus == BookingStatus.Confirmed),
            Arg.Any<CancellationToken>());
    }

    // ── Test 6: Handler publishes notification on reject ──

    [Fact]
    public async Task Handle_RejectAction_PublishesNotification()
    {
        SetupValidSignerAndTenant();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingVerification)
            .WithAmount(50000)
            .WithCustomerEmail("test@example.com")
            .Build();

        var bookingType = CreateBookingType();

        _bookingRepo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var handler = CreateHandler();
        var command = CreateRejectCommand();

        await handler.Handle(command, CancellationToken.None);

        await _publisher.Received(1).Publish(
            Arg.Is<BookingStatusChangedNotification>(n =>
                n.BookingId == BookingId &&
                n.TenantId == TenantId &&
                n.FromStatus == BookingStatus.PendingVerification &&
                n.ToStatus == BookingStatus.Cancelled),
            Arg.Any<CancellationToken>());
    }

    // ── Validator Tests ────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("Approve")]
    [InlineData("REJECT")]
    public void Validator_RejectsInvalidActionValues(string action)
    {
        var validator = new VerifyBookingPaymentCommandValidator();
        var command = new VerifyBookingPaymentCommand
        {
            TenantSlug = TenantSlug,
            BookingId = BookingId,
            Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            Signature = "valid-sig",
            Action = action
        };

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Action);
    }

    [Fact]
    public void Validator_RejectsNoteOver500Chars()
    {
        var validator = new VerifyBookingPaymentCommandValidator();
        var command = new VerifyBookingPaymentCommand
        {
            TenantSlug = TenantSlug,
            BookingId = BookingId,
            Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            Signature = "valid-sig",
            Action = "approve",
            Note = new string('x', 501)
        };

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Note);
    }

    [Fact]
    public void Validator_AcceptsValidApproveCommand()
    {
        var validator = new VerifyBookingPaymentCommandValidator();
        var command = new VerifyBookingPaymentCommand
        {
            TenantSlug = TenantSlug,
            BookingId = BookingId,
            Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            Signature = "valid-sig",
            Action = "approve",
            Note = "Looks good"
        };

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_AcceptsValidRejectCommand()
    {
        var validator = new VerifyBookingPaymentCommandValidator();
        var command = new VerifyBookingPaymentCommand
        {
            TenantSlug = TenantSlug,
            BookingId = BookingId,
            Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            Signature = "valid-sig",
            Action = "reject",
            Note = null
        };

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
