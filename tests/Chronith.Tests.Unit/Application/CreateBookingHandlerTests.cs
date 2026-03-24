using Chronith.Application.Commands.Bookings;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReceivedExtensions;

namespace Chronith.Tests.Unit.Application;

/// <summary>
/// Unit tests for <see cref="CreateBookingHandler"/>.
/// </summary>
public sealed class CreateBookingHandlerTests
{
    // ── Shared scaffolding ────────────────────────────────────────────────────

    private static readonly Guid TenantId = Guid.NewGuid();
    private const string Slug = "test-slot";

    // A fixed UTC start time used across all tests (Monday 2026-03-02 10:00 UTC).
    private static readonly DateTimeOffset FixedStart =
        new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Returns a TimeSlotBookingType with availability windows covering all days 00:00–23:00,
    /// so any reasonable test start time is valid.
    /// </summary>
    private static TimeSlotBookingType BuildTimeSlotWithAllDayWindows(
        PaymentMode paymentMode = PaymentMode.Manual,
        string? paymentProvider = null,
        long priceInCentavos = 50000)
    {
        var allDayWindows = Enum.GetValues<DayOfWeek>()
            .Select(d => new TimeSlotWindow(d, new TimeOnly(0, 0), new TimeOnly(23, 0)))
            .ToList();

        return BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows: allDayWindows,
            tenantId: TenantId,
            paymentMode: paymentMode,
            paymentProvider: paymentProvider,
            priceInCentavos: priceInCentavos);
    }

    /// <summary>
    /// Builds a fully-wired handler with all collaborators substituted.
    /// Returns the handler, the IUnitOfWork mock, the IBookingRepository mock,
    /// the IBookingUrlSigner mock, and the IBookingMetrics mock.
    /// </summary>
    private static (
        CreateBookingHandler Handler,
        IUnitOfWork UnitOfWork,
        IBookingRepository BookingRepo,
        IBookingUrlSigner Signer,
        IBookingMetrics Metrics)
        Build(BookingType bookingType)
    {
        var tenantCtx = Substitute.For<ITenantContext>();
        tenantCtx.TenantId.Returns(TenantId);
        tenantCtx.UserId.Returns("user-1");

        var tenant = Tenant.Create("tenant-slug", "Tenant", "UTC");

        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        bookingTypeRepo
            .GetBySlugAsync(TenantId, Slug, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var bookingRepo = Substitute.For<IBookingRepository>();
        bookingRepo
            .CountConflictsAsync(
                bookingType.Id,
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<IReadOnlyList<BookingStatus>>(),
                Arg.Any<CancellationToken>())
            .Returns(0);

        var tenantRepo = Substitute.For<ITenantRepository>();
        tenantRepo.GetByIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(tenant);

        var tx = Substitute.For<IUnitOfWorkTransaction>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(tx);

        var publisher = Substitute.For<IPublisher>();

        var signer = Substitute.For<IBookingUrlSigner>();
        signer.GenerateSignedUrl(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(ci => $"https://test.com/pay?bookingId={ci.ArgAt<Guid>(1)}&tenantSlug={ci.ArgAt<string>(2)}&expires=999&sig=abc");

        var pageOptions = Options.Create(new PaymentPageOptions { BaseUrl = "https://test.com/pay" });

        var metrics = Substitute.For<IBookingMetrics>();

        var handler = new CreateBookingHandler(
            tenantCtx,
            bookingTypeRepo,
            bookingRepo,
            tenantRepo,
            unitOfWork,
            publisher,
            signer,
            pageOptions,
            metrics);

        return (handler, unitOfWork, bookingRepo, signer, metrics);
    }

    private static CreateBookingCommand MakeCommand() => new()
    {
        BookingTypeSlug = Slug,
        StartTime = FixedStart,
        CustomerEmail = "customer@example.com"
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AutomaticPaidBooking_ReturnsPaymentUrl()
    {
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, _, _, _) = Build(bookingType);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.PaymentUrl.Should().NotBeNullOrEmpty();
        result.PaymentUrl.Should().Contain("bookingId=");
        result.PaymentUrl.Should().Contain("tenantSlug=");
        result.PaymentUrl.Should().Contain("sig=");
    }

    [Fact]
    public async Task Handle_AutomaticPaidBooking_DoesNotCreateCheckoutSession()
    {
        // Checkout sessions are now created on-demand, not at booking creation
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, bookingRepo, _, _) = Build(bookingType);

        await handler.Handle(MakeCommand(), CancellationToken.None);

        // No UpdateAsync call for checkout details since no checkout is created
        await bookingRepo.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AutomaticPaidBooking_ReturnedDtoHasNullPaymentReference()
    {
        // Payment reference is set later when checkout is created on-demand
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, _, _, _) = Build(bookingType);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.PaymentReference.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AutomaticPaidBooking_ReturnedDtoHasNullCheckoutUrl()
    {
        // Checkout URL is set later when checkout is created on-demand
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, _, _, _) = Build(bookingType);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.CheckoutUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ManualPaidBooking_ReturnsPaymentUrl()
    {
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Manual);
        var (handler, _, _, _, _) = Build(bookingType);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.PaymentUrl.Should().NotBeNullOrEmpty(
            "any paid booking should get a payment URL regardless of payment mode");
    }

    [Fact]
    public async Task Handle_ManualPaymentMode_ReturnedDtoHasNullCheckoutUrl()
    {
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Manual);
        var (handler, _, _, _, _) = Build(bookingType);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.CheckoutUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_FreeBooking_PaymentUrlIsNull()
    {
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub", priceInCentavos: 0);
        var (handler, _, _, _, _) = Build(bookingType);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.PaymentUrl.Should().BeNull();
        result.Status.Should().Be(BookingStatus.PendingVerification);
    }

    [Fact]
    public async Task Handle_ManualPaymentMode_RecordsBookingCreatedMetric()
    {
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Manual);
        var (handler, _, _, _, metrics) = Build(bookingType);

        await handler.Handle(MakeCommand(), CancellationToken.None);

        metrics.Received(1).RecordBookingCreated(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_AutomaticPaidBooking_RecordsBookingCreatedMetric()
    {
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, _, _, metrics) = Build(bookingType);

        await handler.Handle(MakeCommand(), CancellationToken.None);

        metrics.Received(1).RecordBookingCreated(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_AutomaticPaidBooking_StatusIsPendingPayment()
    {
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, _, _, _) = Build(bookingType);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.Status.Should().Be(BookingStatus.PendingPayment);
    }
}
