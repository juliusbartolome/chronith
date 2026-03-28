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

    private static (PublicCreateBookingHandler Handler, IBookingUrlSigner Signer, ICustomerRepository CustomerRepo, IBookingRepository BookingRepo) Build(
        BookingType bookingType,
        Customer? existingCustomer = null)
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

        var customerRepo = Substitute.For<ICustomerRepository>();
        customerRepo.GetByEmailAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(existingCustomer);

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
            customerRepo,
            unitOfWork,
            publisher,
            signer,
            pageOptions);

        return (handler, signer, customerRepo, bookingRepo);
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
        var (handler, _, _, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.PaymentUrl.Should().NotBeNullOrEmpty();
        result.PaymentUrl.Should().Contain("bookingId=");
    }

    [Fact]
    public async Task Handle_FreeBooking_NoPaymentUrl()
    {
        var bt = BuildTimeSlot(PaymentMode.Automatic, "Stub", priceInCentavos: 0);
        var (handler, _, _, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.PaymentUrl.Should().BeNull();
        result.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Handle_ManualPaidBooking_ReturnsPaymentUrl()
    {
        var bt = BuildTimeSlot(PaymentMode.Manual);
        var (handler, _, _, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.PaymentUrl.Should().NotBeNullOrEmpty(
            "any paid booking should get a payment URL regardless of payment mode");
        result.PaymentUrl.Should().Contain("bookingId=");
    }

    [Fact]
    public async Task Handle_AutomaticPaidBooking_StatusIsPendingPayment()
    {
        var bt = BuildTimeSlot(PaymentMode.Automatic, "PayMongo");
        var (handler, _, _, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.Status.Should().Be(BookingStatus.PendingPayment);
    }

    [Fact]
    public async Task Handle_AutomaticPaidBooking_CheckoutUrlIsNull()
    {
        var bt = BuildTimeSlot(PaymentMode.Automatic, "PayMongo");
        var (handler, _, _, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.CheckoutUrl.Should().BeNull("checkout is created on-demand, not at booking creation");
    }

    // ── Customer Upsert Tests ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithContactFields_NoExistingCustomer_CreatesCustomer()
    {
        var bt = BuildTimeSlot(priceInCentavos: 0);
        var (handler, _, customerRepo, _) = Build(bt);

        var cmd = MakeCommand() with
        {
            FirstName = "Julius",
            LastName = "Bartolome",
            Mobile = "+639171234567"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        await customerRepo.Received(1).AddAsync(
            Arg.Is<Customer>(c =>
                c.FirstName == "Julius" &&
                c.LastName == "Bartolome" &&
                c.Mobile == "+639171234567" &&
                c.AuthProvider == "public" &&
                c.Email == "customer@example.com" &&
                !c.IsEmailVerified),
            Arg.Any<CancellationToken>());

        result.CustomerAccountId.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithContactFields_ExistingCustomer_UpdatesCustomer()
    {
        var bt = BuildTimeSlot(priceInCentavos: 0);
        var existing = Customer.Create(TenantId, "customer@example.com", null,
            "Old", "Name", "+639170000000", "public");
        var (handler, _, customerRepo, _) = Build(bt, existingCustomer: existing);

        var cmd = MakeCommand() with
        {
            FirstName = "Julius",
            LastName = "Bartolome",
            Mobile = "+639171234567"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        customerRepo.Received(1).Update(
            Arg.Is<Customer>(c =>
                c.FirstName == "Julius" &&
                c.LastName == "Bartolome" &&
                c.Mobile == "+639171234567"));

        await customerRepo.DidNotReceive().AddAsync(
            Arg.Any<Customer>(), Arg.Any<CancellationToken>());

        result.CustomerAccountId.Should().Be(existing.Id);
    }

    [Fact]
    public async Task Handle_WithoutContactFields_NoCustomerUpsert()
    {
        var bt = BuildTimeSlot(priceInCentavos: 0);
        var (handler, _, customerRepo, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        await customerRepo.DidNotReceive().AddAsync(
            Arg.Any<Customer>(), Arg.Any<CancellationToken>());
        customerRepo.DidNotReceive().Update(Arg.Any<Customer>());

        result.CustomerAccountId.Should().BeNull();
    }
}
