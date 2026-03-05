using Chronith.Application.Interfaces;
using Chronith.Application.Notifications;
using Chronith.Domain.Enums;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class CacheInvalidationHandlerTests
{
    private static BookingStatusChangedNotification BuildNotification(Guid tenantId) =>
        new(
            BookingId: Guid.NewGuid(),
            TenantId: tenantId,
            BookingTypeId: Guid.NewGuid(),
            BookingTypeSlug: "consult",
            FromStatus: null,
            ToStatus: BookingStatus.PendingPayment,
            Start: DateTimeOffset.UtcNow,
            End: DateTimeOffset.UtcNow.AddHours(1),
            CustomerId: "cust-1",
            CustomerEmail: "test@example.com");

    [Fact]
    public async Task Handle_WithCache_InvalidatesMetricsCacheKey()
    {
        var tenantId = Guid.NewGuid();
        var cacheService = Substitute.For<IRedisCacheService>();
        var handler = new CacheInvalidationHandler(cacheService);

        await handler.Handle(BuildNotification(tenantId), CancellationToken.None);

        await cacheService.Received(1)
            .InvalidateAsync($"metrics:{tenantId}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNullCache_DoesNotThrow()
    {
        var handler = new CacheInvalidationHandler(null);

        var act = async () => await handler.Handle(
            BuildNotification(Guid.NewGuid()), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WithCache_UsesCorrectTenantIdInKey()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var cacheService = Substitute.For<IRedisCacheService>();
        var handler = new CacheInvalidationHandler(cacheService);

        await handler.Handle(BuildNotification(tenantA), CancellationToken.None);
        await handler.Handle(BuildNotification(tenantB), CancellationToken.None);

        await cacheService.Received(1)
            .InvalidateAsync($"metrics:{tenantA}", Arg.Any<CancellationToken>());
        await cacheService.Received(1)
            .InvalidateAsync($"metrics:{tenantB}", Arg.Any<CancellationToken>());
    }
}
