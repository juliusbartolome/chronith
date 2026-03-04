using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Availability;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class GetAvailabilityHandlerCacheTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WithCacheHit_SkipsRepositories()
    {
        var query = new GetAvailabilityQuery
        {
            BookingTypeSlug = "consult",
            From = DateTimeOffset.UtcNow,
            To = DateTimeOffset.UtcNow.AddDays(1)
        };

        var cached = new AvailabilityDto(
            new List<AvailableSlotDto>
            {
                new(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))
            });

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId);

        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        var bookingRepo = Substitute.For<IBookingRepository>();
        var tenantRepo = Substitute.For<ITenantRepository>();
        var slotGenerator = Substitute.For<ISlotGeneratorService>();

        var cacheService = Substitute.For<IRedisCacheService>();
        cacheService
            .GetOrSetAsync<AvailabilityDto>(
                Arg.Any<string>(),
                Arg.Any<Func<Task<AvailabilityDto>>>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(cached);

        var handler = new GetAvailabilityHandler(
            tenantContext, bookingTypeRepo, bookingRepo, tenantRepo, slotGenerator, cacheService);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().Be(cached);
        await bookingTypeRepo.DidNotReceive()
            .GetBySlugAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
