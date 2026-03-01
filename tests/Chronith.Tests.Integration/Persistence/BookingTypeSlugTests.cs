using Chronith.Tests.Integration.Fixtures;
using Chronith.Tests.Integration.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class BookingTypeSlugTests(PostgresFixture postgres)
{
    [Fact]
    public async Task SlugLookup_FindsBookingType_ByTenantAndSlug()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.Container.GetConnectionString(), tenantId, applyMigrations: true);

        await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, tenantId, slug: "my-service");

        // Act
        var found = await db.BookingTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(bt => bt.TenantId == tenantId && bt.Slug == "my-service");

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(bookingTypeId);
        found.Slug.Should().Be("my-service");
    }

    [Fact]
    public async Task SlugLookup_ReturnsNull_ForDifferentTenant()
    {
        // Arrange — seed the booking type under tenantA
        var tenantIdA = Guid.NewGuid();
        var tenantIdB = Guid.NewGuid();

        await using var dbA = await DbContextFactory.CreateAsync(
            postgres.Container.GetConnectionString(), tenantIdA, applyMigrations: true);

        await SeedData.SeedTenantAsync(dbA, $"tenant-{tenantIdA:N}");
        await SeedData.SeedBookingTypeAsync(dbA, tenantIdA, slug: "shared-slug");

        // Act — look up the same slug but scoped to tenantB's context
        await using var dbB = await DbContextFactory.CreateAsync(
            postgres.Container.GetConnectionString(), tenantIdB);

        // The global query filter on dbB restricts to tenantIdB — nothing should match
        var found = await dbB.BookingTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(bt => bt.Slug == "shared-slug");

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task DuplicateSlug_SameTenant_ThrowsOnSave()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.Container.GetConnectionString(), tenantId, applyMigrations: true);

        await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        await SeedData.SeedBookingTypeAsync(db, tenantId, slug: "duplicate-slug");

        // Act — attempt to insert a second booking type with the same tenant+slug
        var act = async () =>
            await SeedData.SeedBookingTypeAsync(db, tenantId, slug: "duplicate-slug");

        // Assert — unique index violation
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
