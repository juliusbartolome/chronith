using Chronith.Application.Commands.Bookings;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using MediatR;
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
    /// the IPaymentProvider mock, and the IBookingMetrics mock.
    /// </summary>
    private static (
        CreateBookingHandler Handler,
        IUnitOfWork UnitOfWork,
        IBookingRepository BookingRepo,
        IPaymentProvider Provider,
        IBookingMetrics Metrics)
        Build(BookingType bookingType, bool resolverReturnsNull = false)
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

        var provider = Substitute.For<IPaymentProvider>();
        provider.ProviderName.Returns("Stub");
        provider
            .CreateCheckoutSessionAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateCheckoutResult("https://pay.example.com/checkout/123", "ext-id-123"));

        var resolver = Substitute.For<ITenantPaymentProviderResolver>();
        resolver
            .ResolveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(resolverReturnsNull ? (IPaymentProvider?)null : provider);

        var metrics = Substitute.For<IBookingMetrics>();

        var handler = new CreateBookingHandler(
            tenantCtx,
            bookingTypeRepo,
            bookingRepo,
            tenantRepo,
            unitOfWork,
            publisher,
            resolver,
            metrics);

        return (handler, unitOfWork, bookingRepo, provider, metrics);
    }

    private static CreateBookingCommand MakeCommand() => new()
    {
        BookingTypeSlug = Slug,
        StartTime = FixedStart,
        CustomerEmail = "customer@example.com"
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AutomaticPaymentMode_CallsCreateCheckoutSessionAsync()
    {
        // Arrange
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, _, provider, _) = Build(bookingType);

        // Act
        await handler.Handle(MakeCommand(), CancellationToken.None);

        // Assert
        await provider.Received(1)
            .CreateCheckoutSessionAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AutomaticPaymentMode_CallsUpdateAsyncAfterCheckoutSessionCreation()
    {
        // Arrange
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, bookingRepo, _, _) = Build(bookingType);

        // Act
        await handler.Handle(MakeCommand(), CancellationToken.None);

        // Assert — UpdateAsync must be called so that PaymentReference and CheckoutUrl
        // are persisted via ExecuteUpdateAsync.
        await bookingRepo.Received().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AutomaticPaymentMode_ReturnedDtoContainsPaymentReference()
    {
        // Arrange
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, _, _, _) = Build(bookingType);

        // Act
        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        // Assert — the DTO returned to the caller must reflect the ProviderTransactionId
        result.PaymentReference.Should().Be("ext-id-123");
    }

    [Fact]
    public async Task Handle_AutomaticPaymentMode_ReturnedDtoContainsCheckoutUrl()
    {
        // Arrange
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, _, _, _) = Build(bookingType);

        // Act
        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        // Assert — the DTO returned to the caller must include the CheckoutUrl
        result.CheckoutUrl.Should().Be("https://pay.example.com/checkout/123");
    }

    [Fact]
    public async Task Handle_ManualPaymentMode_DoesNotCallPaymentProvider()
    {
        // Arrange — Manual mode
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Manual);
        var (handler, _, _, provider, _) = Build(bookingType);

        // Act
        await handler.Handle(MakeCommand(), CancellationToken.None);

        // Assert
        await provider.DidNotReceive()
            .CreateCheckoutSessionAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ManualPaymentMode_ReturnedDtoHasNullCheckoutUrl()
    {
        // Arrange — Manual mode never calls payment provider
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Manual);
        var (handler, _, _, _, _) = Build(bookingType);

        // Act
        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        // Assert
        result.CheckoutUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AutomaticPaymentMode_FreeBooking_DoesNotCallPaymentProvider()
    {
        // Arrange — Automatic mode but price is 0 (free)
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub", priceInCentavos: 0);
        var (handler, _, _, provider, _) = Build(bookingType);

        // Act
        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        // Assert — Free bookings skip the checkout session entirely
        await provider.DidNotReceive()
            .CreateCheckoutSessionAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>());
        result.Status.Should().Be(BookingStatus.PendingVerification);
        result.CheckoutUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AutomaticPaymentMode_PassesCorrectCheckoutRequest()
    {
        // Arrange
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, _, provider, _) = Build(bookingType);

        // Act
        await handler.Handle(MakeCommand(), CancellationToken.None);

        // Assert — verify the request params passed to the provider
        await provider.Received(1).CreateCheckoutSessionAsync(
            Arg.Is<CreateCheckoutRequest>(r =>
                r.AmountInCentavos == bookingType.PriceInCentavos &&
                r.Currency == bookingType.Currency &&
                r.TenantId == TenantId &&
                r.Description.Contains(bookingType.Name)),
            Arg.Any<CancellationToken>());
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
    public async Task Handle_AutomaticPaymentMode_RecordsPaymentProcessedMetric()
    {
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, _, _, metrics) = Build(bookingType);

        await handler.Handle(MakeCommand(), CancellationToken.None);

        metrics.Received(1).RecordPaymentProcessed(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_WhenResolverReturnsNull_SkipsCheckoutAndStaysAtPendingPayment()
    {
        // Arrange — Automatic mode but resolver returns null (no active config)
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "PayMongo");
        var (handler, _, bookingRepo, provider, _) = Build(bookingType, resolverReturnsNull: true);

        // Act
        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        // Assert — checkout never called, booking stays PendingPayment
        await provider.DidNotReceive()
            .CreateCheckoutSessionAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>());
        await bookingRepo.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
        result.Status.Should().Be(BookingStatus.PendingPayment);
        result.CheckoutUrl.Should().BeNull();
    }
}
