using Chronith.Application.Commands.Public;
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

namespace Chronith.Tests.Unit.Application;

public sealed class PublicCreateBookingHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string Slug = "test-slot";
    private static readonly DateTimeOffset FixedStart = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    private static TimeSlotBookingType BuildTimeSlot(
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

    private static (PublicCreateBookingHandler Handler, IBookingUrlSigner Signer) Build(
        BookingType bookingType)
    {
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "UTC");

        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        bookingTypeRepo.GetBySlugAsync(TenantId, Slug, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var bookingRepo = Substitute.For<IBookingRepository>();
        bookingRepo.CountConflictsAsync(
            bookingType.Id, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<IReadOnlyList<BookingStatus>>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var tenantRepo = Substitute.For<ITenantRepository>();
        tenantRepo.GetByIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(tenant);

        var tx = Substitute.For<IUnitOfWorkTransaction>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(tx);

        var publisher = Substitute.For<IPublisher>();

        var signer = Substitute.For<IBookingUrlSigner>();
        signer.GenerateSignedUrl(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(ci => $"https://test.com/pay?bookingId={ci.ArgAt<Guid>(1)}&tenantSlug={ci.ArgAt<string>(2)}&sig=test");

        var pageOptions = Options.Create(new PaymentPageOptions { BaseUrl = "https://test.com/pay" });

        var handler = new PublicCreateBookingHandler(
            bookingTypeRepo,
            bookingRepo,
            tenantRepo,
            unitOfWork,
            publisher,
            signer,
            pageOptions);

        return (handler, signer);
    }

    private static PublicCreateBookingCommand MakeCommand() => new()
    {
        TenantId = TenantId,
        BookingTypeSlug = Slug,
        StartTime = FixedStart,
        CustomerEmail = "customer@example.com",
        CustomerId = "cust-1"
    };

    [Fact]
    public async Task Handle_AutomaticPaidBooking_ReturnsPaymentUrl()
    {
        var bt = BuildTimeSlot(PaymentMode.Automatic, "PayMongo");
        var (handler, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.PaymentUrl.Should().NotBeNullOrEmpty();
        result.PaymentUrl.Should().Contain("bookingId=");
    }

    [Fact]
    public async Task Handle_FreeBooking_NoPaymentUrl()
    {
        var bt = BuildTimeSlot(PaymentMode.Automatic, "Stub", priceInCentavos: 0);
        var (handler, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.PaymentUrl.Should().BeNull();
        result.Status.Should().Be(BookingStatus.PendingVerification);
    }

    [Fact]
    public async Task Handle_ManualMode_NoPaymentUrl()
    {
        var bt = BuildTimeSlot(PaymentMode.Manual);
        var (handler, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.PaymentUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AutomaticPaidBooking_StatusIsPendingPayment()
    {
        var bt = BuildTimeSlot(PaymentMode.Automatic, "PayMongo");
        var (handler, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.Status.Should().Be(BookingStatus.PendingPayment);
    }

    [Fact]
    public async Task Handle_AutomaticPaidBooking_CheckoutUrlIsNull()
    {
        var bt = BuildTimeSlot(PaymentMode.Automatic, "PayMongo");
        var (handler, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.CheckoutUrl.Should().BeNull("checkout is created on-demand, not at booking creation");
    }
}
