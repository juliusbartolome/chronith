using Chronith.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migration_AppliesCleanly_ToFreshDatabase()
    {
        // Arrange & Act
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString,
            Guid.NewGuid(),
            applyMigrations: true);

        // Assert
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
        pendingMigrations.Should().BeEmpty();
    }

    [Fact]
    public async Task Migration_IsIdempotent_WhenAppliedTwice()
    {
        // Arrange
        var cs = postgres.ConnectionString;
        await using var db1 = await DbContextFactory.CreateAsync(cs, Guid.NewGuid(), applyMigrations: true);

        // Act — apply again
        await db1.Database.MigrateAsync();

        // Assert
        var pendingMigrations = await db1.Database.GetPendingMigrationsAsync();
        pendingMigrations.Should().BeEmpty();
    }
}
