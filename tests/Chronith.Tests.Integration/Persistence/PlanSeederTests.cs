using Chronith.Infrastructure.Persistence.Seeding;
using Chronith.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public class PlanSeederTests(PostgresFixture postgres)
{
    [Fact]
    public async Task SeedAsync_InsertsAllFourDefaultPlans()
    {
        // Use a unique schema per test to avoid inter-test state
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PlanSeeder>.Instance;
        var seeder = new PlanSeeder(db, logger);

        await seeder.SeedAsync(CancellationToken.None);

        var plans = await db.TenantPlans
            .IgnoreQueryFilters()
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        plans.Should().HaveCount(4);

        plans[0].Id.Should().Be(PlanSeeder.FreePlanId);
        plans[0].Name.Should().Be("Free");
        plans[0].PriceCentavos.Should().Be(0);
        plans[0].MaxBookingTypes.Should().Be(1);
        plans[0].MaxStaffMembers.Should().Be(0);
        plans[0].MaxBookingsPerMonth.Should().Be(50);
        plans[0].MaxCustomers.Should().Be(50);
        plans[0].IsActive.Should().BeTrue();

        plans[1].Id.Should().Be(PlanSeeder.StarterPlanId);
        plans[1].Name.Should().Be("Starter");
        plans[1].PriceCentavos.Should().Be(190000);

        plans[2].Id.Should().Be(PlanSeeder.ProPlanId);
        plans[2].Name.Should().Be("Pro");
        plans[2].PriceCentavos.Should().Be(490000);

        plans[3].Id.Should().Be(PlanSeeder.EnterprisePlanId);
        plans[3].Name.Should().Be("Enterprise");
        plans[3].PriceCentavos.Should().Be(1490000);
        plans[3].MaxBookingTypes.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_DoesNotInsertDuplicates()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PlanSeeder>.Instance;
        var seeder = new PlanSeeder(db, logger);

        // Seed twice
        await seeder.SeedAsync(CancellationToken.None);
        await seeder.SeedAsync(CancellationToken.None);

        var count = await db.TenantPlans
            .IgnoreQueryFilters()
            .CountAsync();

        count.Should().Be(4);
    }
}
