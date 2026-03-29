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

public sealed class GetPublicBookingStatusQueryHandlerTests
{
    private readonly IBookingRepository _bookingRepo = Substitute.For<IBookingRepository>();
    private readonly IBookingTypeRepository _bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
    private readonly ITenantPaymentConfigRepository _paymentConfigRepo = Substitute.For<ITenantPaymentConfigRepository>();

    private GetPublicBookingStatusQueryHandler CreateHandler()
        => new(_bookingRepo, _bookingTypeRepo, _paymentConfigRepo);

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BookingTypeId = Guid.NewGuid();

    private static TimeSlotBookingType CreateBookingType(PaymentMode paymentMode)
        => TimeSlotBookingType.Create(
            tenantId: TenantId,
            slug: "test-type",
            name: "Test Type",
            capacity: 1,
            paymentMode: paymentMode,
            paymentProvider: paymentMode == PaymentMode.Automatic ? "PayMongo" : null,
            durationMinutes: 60,
            bufferBeforeMinutes: 0,
            bufferAfterMinutes: 0,
            availabilityWindows: [],
            priceInCentavos: 50000,
            currency: "PHP");

    [Fact]
    public async Task Handle_ReturnsDtoWithCheckoutUrl_WhenStatusIsPendingPayment()
    {
        var bookingId = Guid.NewGuid();
        var checkoutUrl = "https://checkout.paymongo.com/session_abc";

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(bookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingPayment)
            .WithAmount(50000)
            .WithPaymentReference("cs_live_abc123")
            .WithCheckoutUrl(checkoutUrl)
            .Build();

        var bookingType = CreateBookingType(PaymentMode.Automatic);

        _bookingRepo.GetPublicByIdAsync(TenantId, bookingId, Arg.Any<CancellationToken>())
             .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
             .Returns(bookingType);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GetPublicBookingStatusQuery(TenantId, bookingId), CancellationToken.None);

        result.Status.Should().Be(BookingStatus.PendingPayment);
        result.ReferenceId.Should().Be(bookingId.ToString("N"));
        result.CheckoutUrl.Should().Be(checkoutUrl);
        result.PaymentReference.Should().Be("cs_live_abc123");
        result.AmountInCentavos.Should().Be(50000L);
        result.Currency.Should().Be("PHP");
    }

    [Fact]
    public async Task Handle_NullsOutCheckoutUrl_WhenStatusIsNotPendingPayment()
    {
        var bookingId = Guid.NewGuid();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(bookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.Confirmed)
            .WithCheckoutUrl("https://should-not-be-returned.example.com")
            .Build();

        var bookingType = CreateBookingType(PaymentMode.Automatic);

        _bookingRepo.GetPublicByIdAsync(TenantId, bookingId, Arg.Any<CancellationToken>())
             .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
             .Returns(bookingType);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GetPublicBookingStatusQuery(TenantId, bookingId), CancellationToken.None);

        result.Status.Should().Be(BookingStatus.Confirmed);
        result.CheckoutUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenBookingNotFound()
    {
        var bookingId = Guid.NewGuid();

        _bookingRepo.GetPublicByIdAsync(TenantId, bookingId, Arg.Any<CancellationToken>())
             .Returns(default(Booking));

        var handler = CreateHandler();
        var act = () => handler.Handle(
            new GetPublicBookingStatusQuery(TenantId, bookingId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── New tests (4d) ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsManualPaymentMode_WhenBookingTypeIsManual()
    {
        var bookingId = Guid.NewGuid();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(bookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingPayment)
            .WithAmount(50000)
            .Build();

        var bookingType = CreateBookingType(PaymentMode.Manual);

        _bookingRepo.GetPublicByIdAsync(TenantId, bookingId, Arg.Any<CancellationToken>())
             .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
             .Returns(bookingType);
        _paymentConfigRepo.GetActiveByProviderNameAsync(TenantId, "Manual", Arg.Any<CancellationToken>())
             .Returns(TenantPaymentConfig.Create(TenantId, "Manual", "GCash", "{}", "Send to 09171234567", "https://example.com/qr.png"));

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GetPublicBookingStatusQuery(TenantId, bookingId), CancellationToken.None);

        result.PaymentMode.Should().Be("Manual");
    }

    [Fact]
    public async Task Handle_ReturnsManualPaymentOptions_WithQrCodeAndNote()
    {
        var bookingId = Guid.NewGuid();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(bookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingPayment)
            .WithAmount(50000)
            .Build();

        var bookingType = CreateBookingType(PaymentMode.Manual);
        var config = TenantPaymentConfig.Create(
            TenantId, "Manual", "GCash", "{}",
            publicNote: "Send to 09171234567",
            qrCodeUrl: "https://example.com/qr.png");

        _bookingRepo.GetPublicByIdAsync(TenantId, bookingId, Arg.Any<CancellationToken>())
             .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
             .Returns(bookingType);
        _paymentConfigRepo.GetActiveByProviderNameAsync(TenantId, "Manual", Arg.Any<CancellationToken>())
             .Returns(config);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GetPublicBookingStatusQuery(TenantId, bookingId), CancellationToken.None);

        result.ManualPaymentOptions.Should().NotBeNull();
        result.ManualPaymentOptions!.QrCodeUrl.Should().Be("https://example.com/qr.png");
        result.ManualPaymentOptions.PublicNote.Should().Be("Send to 09171234567");
        result.ManualPaymentOptions.Label.Should().Be("GCash");
    }

    [Fact]
    public async Task Handle_ReturnsNullManualPaymentOptions_WhenPaymentModeIsAutomatic()
    {
        var bookingId = Guid.NewGuid();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(bookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingPayment)
            .WithAmount(50000)
            .Build();

        var bookingType = CreateBookingType(PaymentMode.Automatic);

        _bookingRepo.GetPublicByIdAsync(TenantId, bookingId, Arg.Any<CancellationToken>())
             .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
             .Returns(bookingType);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GetPublicBookingStatusQuery(TenantId, bookingId), CancellationToken.None);

        result.PaymentMode.Should().Be("Automatic");
        result.ManualPaymentOptions.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MapsProofFields_Correctly()
    {
        var bookingId = Guid.NewGuid();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(bookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingVerification)
            .WithAmount(50000)
            .WithProofOfPaymentUrl("https://storage.example.com/proof.jpg")
            .WithProofOfPaymentFileName("proof.jpg")
            .WithPaymentNote("Sent via GCash at 3pm")
            .Build();

        var bookingType = CreateBookingType(PaymentMode.Manual);

        _bookingRepo.GetPublicByIdAsync(TenantId, bookingId, Arg.Any<CancellationToken>())
             .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
             .Returns(bookingType);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GetPublicBookingStatusQuery(TenantId, bookingId), CancellationToken.None);

        result.ProofOfPaymentUrl.Should().Be("https://storage.example.com/proof.jpg");
        result.ProofOfPaymentFileName.Should().Be("proof.jpg");
        result.PaymentNote.Should().Be("Sent via GCash at 3pm");
    }

    [Fact]
    public async Task Handle_HidesCheckoutUrl_WhenStatusIsNotPendingPayment()
    {
        var bookingId = Guid.NewGuid();

        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(bookingId)
            .WithBookingTypeId(BookingTypeId)
            .InStatus(BookingStatus.PendingVerification)
            .WithAmount(50000)
            .WithCheckoutUrl("https://checkout.paymongo.com/cs_123")
            .Build();

        var bookingType = CreateBookingType(PaymentMode.Automatic);

        _bookingRepo.GetPublicByIdAsync(TenantId, bookingId, Arg.Any<CancellationToken>())
             .Returns(booking);
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
             .Returns(bookingType);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new GetPublicBookingStatusQuery(TenantId, bookingId), CancellationToken.None);

        result.CheckoutUrl.Should().BeNull();
        result.Status.Should().Be(BookingStatus.PendingVerification);
    }
}
