using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Tenant.GetTenantMetrics;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class GetTenantMetricsQueryHandlerCacheTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static (GetTenantMetricsQueryHandler Handler,
        IBookingRepository BookingRepo,
        IRedisCacheService CacheService)
        Build(IRedisCacheService? cacheService = null)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId);

        var tenantRepo = Substitute.For<ITenantRepository>();
        tenantRepo.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(Tenant.Create("slug", "Test Tenant", "UTC"));

        var bookingRepo = Substitute.For<IBookingRepository>();
        bookingRepo.GetMetricsAsync(TenantId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new BookingMetrics(0, new Dictionary<BookingStatus, int>(), 0));

        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        outboxRepo.GetDeliveryMetricsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new DeliveryMetrics(0, 0));

        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        bookingTypeRepo.GetTypeMetricsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new BookingTypeMetrics(0, 0));

        var cache = cacheService ?? Substitute.For<IRedisCacheService>();

        var handler = new GetTenantMetricsQueryHandler(
            tenantContext, bookingRepo, outboxRepo, bookingTypeRepo, tenantRepo, cache);

        return (handler, bookingRepo, cache);
    }

    [Fact]
    public async Task Handle_WithCacheHit_SkipsRepositories()
    {
        var cached = new TenantMetricsDto(
            new BookingMetricsDto(99, new Dictionary<string, int>(), 10),
            new WebhookMetricsDto(5, 4, 1, 80m),
            new BookingTypeMetricsDto(3, 1));

        var cacheService = Substitute.For<IRedisCacheService>();
        cacheService
            .GetOrSetAsync<TenantMetricsDto>(
                Arg.Any<string>(),
                Arg.Any<Func<Task<TenantMetricsDto>>>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(cached);

        var (handler, bookingRepo, _) = Build(cacheService);

        var result = await handler.Handle(new GetTenantMetricsQuery(), CancellationToken.None);

        result.Should().Be(cached);
        await bookingRepo.DidNotReceive()
            .GetMetricsAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutCache_CallsRepositoriesDirectly()
    {
        // Build handler without optional cache (null)
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId);

        var tenantRepo = Substitute.For<ITenantRepository>();
        tenantRepo.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(Tenant.Create("slug", "Test Tenant", "UTC"));

        var bookingRepo = Substitute.For<IBookingRepository>();
        bookingRepo.GetMetricsAsync(TenantId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new BookingMetrics(5, new Dictionary<BookingStatus, int>(), 1));

        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        outboxRepo.GetDeliveryMetricsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new DeliveryMetrics(0, 0));

        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        bookingTypeRepo.GetTypeMetricsAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new BookingTypeMetrics(2, 0));

        // Pass null explicitly for optional cache
        var handler = new GetTenantMetricsQueryHandler(
            tenantContext, bookingRepo, outboxRepo, bookingTypeRepo, tenantRepo, null);

        var result = await handler.Handle(new GetTenantMetricsQuery(), CancellationToken.None);

        result.Bookings.Total.Should().Be(5);
        await bookingRepo.Received(1)
            .GetMetricsAsync(TenantId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}
