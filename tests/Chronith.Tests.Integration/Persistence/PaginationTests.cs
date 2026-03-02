using Chronith.Tests.Integration.Fixtures;
using Chronith.Tests.Integration.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class PaginationTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset BaseDate =
        new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListBookings_ReturnsPaginatedResults_InCorrectOrder()
    {
        // Arrange — seed 10 bookings with distinct start times
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, tenantId);

        for (int i = 0; i < 10; i++)
        {
            await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
                start: BaseDate.AddHours(i),
                end: BaseDate.AddHours(i + 1),
                customerId: $"cust-{i:D2}");
        }

        // Act — first page: skip 0, take 5
        var page1 = await db.Bookings
            .AsNoTracking()
            .OrderBy(b => b.Start)
            .Skip(0)
            .Take(5)
            .Select(b => new { b.Start, b.CustomerId })
            .ToListAsync();

        // Assert
        page1.Should().HaveCount(5);
        page1[0].Start.Should().Be(BaseDate.AddHours(0));
        page1[4].Start.Should().Be(BaseDate.AddHours(4));
        // Verify ascending order
        page1.Should().BeInAscendingOrder(b => b.Start);
    }

    [Fact]
    public async Task ListBookings_RespectsPageSize()
    {
        // Arrange — seed 10 bookings
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, tenantId);

        for (int i = 0; i < 10; i++)
        {
            await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
                start: BaseDate.AddHours(i),
                end: BaseDate.AddHours(i + 1),
                customerId: $"cust-{i:D2}");
        }

        // Act — second page: skip 5, take 5
        var page2 = await db.Bookings
            .AsNoTracking()
            .OrderBy(b => b.Start)
            .Skip(5)
            .Take(5)
            .Select(b => new { b.Start, b.CustomerId })
            .ToListAsync();

        // Assert
        page2.Should().HaveCount(5);
        page2[0].Start.Should().Be(BaseDate.AddHours(5));
        page2[4].Start.Should().Be(BaseDate.AddHours(9));
        page2.Should().BeInAscendingOrder(b => b.Start);
    }
}
