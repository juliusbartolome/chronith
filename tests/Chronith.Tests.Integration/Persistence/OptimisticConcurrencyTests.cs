using Chronith.Tests.Integration.Fixtures;
using Chronith.Tests.Integration.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class OptimisticConcurrencyTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Update_WithStaleRowVersion_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange — seed a booking type
        var tenantId = Guid.NewGuid();
        var cs = postgres.Container.GetConnectionString();

        await using var dbSetup = await DbContextFactory.CreateAsync(cs, tenantId, applyMigrations: true);
        await SeedData.SeedTenantAsync(dbSetup, $"tenant-{tenantId:N}");
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(dbSetup, tenantId, slug: "concurrency-test");

        // Load the same row in two separate contexts
        await using var db1 = await DbContextFactory.CreateAsync(cs, tenantId);
        await using var db2 = await DbContextFactory.CreateAsync(cs, tenantId);

        var bt1 = await db1.BookingTypes.FirstAsync(bt => bt.Id == bookingTypeId);
        var bt2 = await db2.BookingTypes.FirstAsync(bt => bt.Id == bookingTypeId);

        // Act — context 1 saves first (wins the race)
        bt1.Name = "Updated by Context 1";
        await db1.SaveChangesAsync();

        // Context 2 tries to save with a now-stale xmin row version
        bt2.Name = "Updated by Context 2";
        var act = async () => await db2.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
