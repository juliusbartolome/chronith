using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Tenant.GetTenantMetrics;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class GetTenantMetricsQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static (GetTenantMetricsQueryHandler Handler,
        IBookingRepository BookingRepo,
        IWebhookOutboxRepository OutboxRepo,
        IBookingTypeRepository BookingTypeRepo)
        Build(
            BookingMetrics? bookingMetrics = null,
            DeliveryMetrics? deliveryMetrics = null,
            BookingTypeMetrics? typeMetrics = null)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId);

        var tenantRepo = Substitute.For<ITenantRepository>();
        tenantRepo.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(Tenant.Create("slug", "Test Tenant", "UTC"));

        var bookingRepo = Substitute.For<IBookingRepository>();
        bookingRepo.GetMetricsAsync(TenantId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(bookingMetrics ?? new BookingMetrics(0, new Dictionary<BookingStatus, int>(), 0));

        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        outboxRepo.GetDeliveryMetricsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(deliveryMetrics ?? new DeliveryMetrics(0, 0));

        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        bookingTypeRepo.GetTypeMetricsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(typeMetrics ?? new BookingTypeMetrics(0, 0));

        var handler = new GetTenantMetricsQueryHandler(
            tenantContext, bookingRepo, outboxRepo, bookingTypeRepo, tenantRepo);

        return (handler, bookingRepo, outboxRepo, bookingTypeRepo);
    }

    [Fact]
    public async Task Handle_WhenNoDeliveries_DeliveryRatePctIsNull()
    {
        var (handler, _, _, _) = Build(deliveryMetrics: new DeliveryMetrics(0, 0));

        var result = await handler.Handle(new GetTenantMetricsQuery(), CancellationToken.None);

        result.Webhooks.DeliveryRatePct.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithDeliveries_ComputesDeliveryRatePct()
    {
        // 96 delivered, 4 failed → 96.0%
        var (handler, _, _, _) = Build(deliveryMetrics: new DeliveryMetrics(96, 4));

        var result = await handler.Handle(new GetTenantMetricsQuery(), CancellationToken.None);

        result.Webhooks.DeliveryRatePct.Should().Be(96.0m);
    }

    [Fact]
    public async Task Handle_BookingStatusKeys_AreSnakeCase()
    {
        var byStatus = new Dictionary<BookingStatus, int>
        {
            { BookingStatus.PendingPayment, 3 },
            { BookingStatus.Confirmed, 10 }
        };
        var (handler, _, _, _) = Build(
            bookingMetrics: new BookingMetrics(13, byStatus, 5));

        var result = await handler.Handle(new GetTenantMetricsQuery(), CancellationToken.None);

        result.Bookings.ByStatus.Should().ContainKey("pending_payment");
        result.Bookings.ByStatus.Should().ContainKey("confirmed");
        result.Bookings.ByStatus["pending_payment"].Should().Be(3);
        result.Bookings.ByStatus["confirmed"].Should().Be(10);
    }

    [Fact]
    public async Task Handle_ReturnsCorrectAggregates()
    {
        var byStatus = new Dictionary<BookingStatus, int>
        {
            { BookingStatus.Confirmed, 5 }
        };
        var (handler, _, _, _) = Build(
            bookingMetrics: new BookingMetrics(5, byStatus, 2),
            deliveryMetrics: new DeliveryMetrics(8, 2),
            typeMetrics: new BookingTypeMetrics(3, 1));

        var result = await handler.Handle(new GetTenantMetricsQuery(), CancellationToken.None);

        result.Bookings.Total.Should().Be(5);
        result.Bookings.ThisMonth.Should().Be(2);
        result.Webhooks.TotalDeliveries.Should().Be(10);
        result.Webhooks.Delivered.Should().Be(8);
        result.Webhooks.Failed.Should().Be(2);
        result.Webhooks.DeliveryRatePct.Should().Be(80.0m);
        result.BookingTypes.Active.Should().Be(3);
        result.BookingTypes.Archived.Should().Be(1);
    }
}
