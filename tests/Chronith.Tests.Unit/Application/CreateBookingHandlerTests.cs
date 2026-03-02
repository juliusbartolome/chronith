using Chronith.Application.Commands.Bookings;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using MediatR;
using NSubstitute;

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
        string? paymentProvider = null)
    {
        var allDayWindows = Enum.GetValues<DayOfWeek>()
            .Select(d => new TimeSlotWindow(d, new TimeOnly(0, 0), new TimeOnly(23, 0)))
            .ToList();

        return BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows: allDayWindows,
            tenantId: TenantId,
            paymentMode: paymentMode,
            paymentProvider: paymentProvider);
    }

    /// <summary>
    /// Builds a fully-wired handler with all collaborators substituted.
    /// Returns the handler, the IUnitOfWork mock, the IBookingRepository mock,
    /// and the IPaymentProvider mock.
    /// </summary>
    private static (
        CreateBookingHandler Handler,
        IUnitOfWork UnitOfWork,
        IBookingRepository BookingRepo,
        IPaymentProvider Provider)
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

        var provider = Substitute.For<IPaymentProvider>();
        provider.ProviderName.Returns("Stub");
        provider
            .CreatePaymentIntentAsync(Arg.Any<Booking>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentIntentResult("ext-id-123", "https://pay.example.com/checkout/123"));

        var providerFactory = Substitute.For<IPaymentProviderFactory>();
        providerFactory.GetProvider(Arg.Any<string>()).Returns(provider);

        var handler = new CreateBookingHandler(
            tenantCtx,
            bookingTypeRepo,
            bookingRepo,
            tenantRepo,
            unitOfWork,
            publisher,
            providerFactory);

        return (handler, unitOfWork, bookingRepo, provider);
    }

    private static CreateBookingCommand MakeCommand() => new()
    {
        BookingTypeSlug = Slug,
        StartTime = FixedStart,
        CustomerEmail = "customer@example.com"
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AutomaticPaymentMode_CallsUpdateAsyncAfterPaymentIntentCreation()
    {
        // Arrange
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, bookingRepo, _) = Build(bookingType);

        // Act
        await handler.Handle(MakeCommand(), CancellationToken.None);

        // Assert — UpdateAsync must be called so that PaymentReference and CheckoutUrl
        // are persisted. The transaction commits before the payment call, so the tracked
        // EF entity does not reflect in-memory changes; ExecuteUpdateAsync (via UpdateAsync)
        // writes the updated fields directly to the database.
        await bookingRepo.Received().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AutomaticPaymentMode_ReturnedDtoContainsPaymentReference()
    {
        // Arrange
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, _, _) = Build(bookingType);

        // Act
        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        // Assert — the DTO returned to the caller must reflect the ExternalId
        result.PaymentReference.Should().Be("ext-id-123");
    }

    [Fact]
    public async Task Handle_AutomaticPaymentMode_ReturnedDtoContainsCheckoutUrl()
    {
        // Arrange
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
        var (handler, _, _, _) = Build(bookingType);

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
        var (handler, _, _, provider) = Build(bookingType);

        // Act
        await handler.Handle(MakeCommand(), CancellationToken.None);

        // Assert
        await provider.DidNotReceive()
            .CreatePaymentIntentAsync(Arg.Any<Booking>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ManualPaymentMode_ReturnedDtoHasNullCheckoutUrl()
    {
        // Arrange — Manual mode never calls payment provider
        var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Manual);
        var (handler, _, _, _) = Build(bookingType);

        // Act
        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        // Assert
        result.CheckoutUrl.Should().BeNull();
    }
}
