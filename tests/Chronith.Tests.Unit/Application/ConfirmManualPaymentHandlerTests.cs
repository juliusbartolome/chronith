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

public sealed class ConfirmManualPaymentHandlerTests
{
    private readonly IBookingUrlSigner _signer = Substitute.For<IBookingUrlSigner>();
    private readonly ITenantRepository _tenantRepo = Substitute.For<ITenantRepository>();
    private readonly IBookingRepository _bookingRepo = Substitute.For<IBookingRepository>();
    private readonly IBookingTypeRepository _bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
    private readonly ITenantPaymentConfigRepository _paymentConfigRepo = Substitute.For<ITenantPaymentConfigRepository>();
    private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();

    private static readonly Guid BookingId = Guid.NewGuid();
    private static readonly Guid BookingTypeId = Guid.NewGuid();
    private const string TenantSlug = "test-tenant";

    // Create tenant once — use its auto-generated Id everywhere
    private static readonly Tenant TestTenant = Tenant.Create(TenantSlug, "Test Tenant", "Asia/Manila");
    private static Guid TenantId => TestTenant.Id;

    private ConfirmManualPaymentCommandHandler CreateHandler()
        => new(_signer, _tenantRepo, _bookingRepo, _bookingTypeRepo, _paymentConfigRepo,
               _fileStorage, _unitOfWork, _publisher);

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
        _signer.Validate(BookingId, TenantSlug, Arg.Any<long>(), Arg.Any<string>())
            .Returns(true);

        _tenantRepo.GetBySlugAsync(TenantSlug, Arg.Any<CancellationToken>())
            .Returns(TestTenant);
    }

    private static ConfirmManualPaymentCommand CreateValidCommand(Stream? proofFile = null) => new()
    {
        TenantSlug = TenantSlug,
        BookingId = BookingId,
        Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
        Signature = "valid-sig",
        ProofFile = proofFile,
        ProofFileName = proofFile is not null ? "receipt.jpg" : null,
        ProofContentType = proofFile is not null ? "image/jpeg" : null,
        PaymentNote = "Paid via GCash"
    };

    // ── Test 1: Handler validates HMAC and throws on invalid signature ──

    [Fact]
    public async Task Handle_InvalidSignature_ThrowsUnauthorizedException()
    {
        _signer.Validate(BookingId, TenantSlug, Arg.Any<long>(), Arg.Any<string>())
            .Returns(false);

        var handler = CreateHandler();
        var command = CreateValidCommand();

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    // ── Test 2: Handler uploads proof file when provided ──

    [Fact]
    public async Task Handle_WithProofFile_UploadsFile()
    {
        SetupValidSignerAndTenant();
        using var stream = new MemoryStream([0x01, 0x02, 0x03]);

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingPayment)
            .WithAmount(50000)
            .Build();

        var bookingType = CreateBookingType();

        _bookingRepo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);
        _fileStorage.UploadAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new FileUploadResult("https://storage.example.com/proof.jpg", "proof.jpg"));

        var handler = CreateHandler();
        var command = new ConfirmManualPaymentCommand
        {
            TenantSlug = TenantSlug,
            BookingId = BookingId,
            Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            Signature = "valid-sig",
            ProofFile = stream,
            ProofFileName = "receipt.jpg",
            ProofContentType = "image/jpeg",
            PaymentNote = "Paid via GCash"
        };

        await handler.Handle(command, CancellationToken.None);

        await _fileStorage.Received(1).UploadAsync(
            $"payment-proofs-{TenantSlug}",
            Arg.Any<string>(),
            stream,
            "image/jpeg",
            Arg.Any<CancellationToken>());
    }

    // ── Test 3: Handler transitions booking to PendingVerification ──

    [Fact]
    public async Task Handle_ValidCommand_TransitionsToPendingVerification()
    {
        SetupValidSignerAndTenant();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingPayment)
            .WithAmount(50000)
            .Build();

        var bookingType = CreateBookingType();

        _bookingRepo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var handler = CreateHandler();
        var command = CreateValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be(BookingStatus.PendingVerification);
        await _bookingRepo.Received(1).UpdatePublicAsync(booking, TenantId, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Test 4: Handler works without proof file ──

    [Fact]
    public async Task Handle_WithoutProofFile_SucceedsWithoutUpload()
    {
        SetupValidSignerAndTenant();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingPayment)
            .WithAmount(50000)
            .Build();

        var bookingType = CreateBookingType();

        _bookingRepo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var handler = CreateHandler();
        var command = new ConfirmManualPaymentCommand
        {
            TenantSlug = TenantSlug,
            BookingId = BookingId,
            Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            Signature = "valid-sig",
            PaymentNote = "Paid via GCash"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be(BookingStatus.PendingVerification);
        await _fileStorage.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Test 5: Handler throws on invalid booking status ──

    [Fact]
    public async Task Handle_BookingNotPendingPayment_ThrowsInvalidStateTransition()
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
        var command = CreateValidCommand();

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    // ── Test 6: Handler publishes notification ──

    [Fact]
    public async Task Handle_ValidCommand_PublishesNotification()
    {
        SetupValidSignerAndTenant();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingPayment)
            .WithAmount(50000)
            .WithCustomerEmail("test@example.com")
            .Build();

        var bookingType = CreateBookingType();

        _bookingRepo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var handler = CreateHandler();
        var command = CreateValidCommand();

        await handler.Handle(command, CancellationToken.None);

        await _publisher.Received(1).Publish(
            Arg.Is<BookingStatusChangedNotification>(n =>
                n.BookingId == BookingId &&
                n.TenantId == TenantId &&
                n.BookingTypeSlug == "manual-type" &&
                n.FromStatus == BookingStatus.PendingPayment &&
                n.ToStatus == BookingStatus.PendingVerification),
            Arg.Any<CancellationToken>());
    }

    // ── Test 7: Validator rejects invalid content types ──

    [Fact]
    public void Validator_RejectsInvalidContentType()
    {
        var validator = new ConfirmManualPaymentCommandValidator();
        var command = new ConfirmManualPaymentCommand
        {
            TenantSlug = TenantSlug,
            BookingId = BookingId,
            Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            Signature = "valid-sig",
            ProofFile = new MemoryStream([0x01]),
            ProofFileName = "doc.pdf",
            ProofContentType = "application/pdf"
        };

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ProofContentType);
    }

    // ── Test 8: Validator rejects notes over 500 chars ──

    [Fact]
    public void Validator_RejectsPaymentNoteOver500Chars()
    {
        var validator = new ConfirmManualPaymentCommandValidator();
        var command = new ConfirmManualPaymentCommand
        {
            TenantSlug = TenantSlug,
            BookingId = BookingId,
            Expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            Signature = "valid-sig",
            PaymentNote = new string('x', 501)
        };

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PaymentNote);
    }
}
