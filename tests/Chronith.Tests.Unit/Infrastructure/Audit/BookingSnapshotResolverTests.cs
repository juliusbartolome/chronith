using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Services.Audit;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure.Audit;

public sealed class BookingSnapshotResolverTests
{
    private readonly IBookingRepository _bookingRepo = Substitute.For<IBookingRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private BookingSnapshotResolver CreateSut() => new(_bookingRepo, _tenantContext);

    [Fact]
    public async Task ResolveSnapshotAsync_WhenBookingExists_ReturnsNonNullJsonContainingExpectedFields()
    {
        var tenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var booking = new BookingBuilder()
            .WithTenantId(tenantId)
            .WithBookingTypeId(Guid.NewGuid())
            .Build();

        _tenantContext.TenantId.Returns(tenantId);
        _bookingRepo.GetByIdAsync(tenantId, bookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        var sut = CreateSut();
        var result = await sut.ResolveSnapshotAsync(bookingId, CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().Contain("\"Id\"");
        result.Should().Contain("\"BookingTypeId\"");
        result.Should().Contain("\"Status\"");
    }

    [Fact]
    public async Task ResolveSnapshotAsync_WhenBookingNotFound_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        _tenantContext.TenantId.Returns(tenantId);
        _bookingRepo.GetByIdAsync(tenantId, bookingId, Arg.Any<CancellationToken>())
            .Returns(default(Booking));

        var sut = CreateSut();
        var result = await sut.ResolveSnapshotAsync(bookingId, CancellationToken.None);

        result.Should().BeNull();
    }
}
