using Chronith.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class IndexVerificationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task BookingIndexes_AllFourCompositeIndexes_ExistAfterMigration()
    {
        // Arrange
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString,
            Guid.NewGuid(),
            applyMigrations: true);

        // Act — query pg_indexes for the bookings table
        var indexes = await db.Database
            .SqlQueryRaw<string>(
                "SELECT indexname FROM pg_indexes WHERE tablename = 'bookings' AND schemaname = 'chronith'")
            .ToListAsync();

        // Assert — all 4 booking composite indexes must exist
        indexes.Should().Contain("ix_bookings_availability");
        indexes.Should().Contain("ix_bookings_customer");
        indexes.Should().Contain("ix_bookings_staff");
        indexes.Should().Contain("ix_bookings_recurrence");
    }

    [Fact]
    public async Task WaitlistIndex_FifoIndex_ExistsAfterMigration()
    {
        // Arrange
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString,
            Guid.NewGuid(),
            applyMigrations: true);

        // Act
        var indexes = await db.Database
            .SqlQueryRaw<string>(
                "SELECT indexname FROM pg_indexes WHERE tablename = 'waitlist_entries' AND schemaname = 'chronith'")
            .ToListAsync();

        // Assert
        indexes.Should().Contain("ix_waitlist_fifo");
    }

    [Fact]
    public async Task IdempotencyIndex_LookupIndex_ExistsAfterMigration()
    {
        // Arrange
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString,
            Guid.NewGuid(),
            applyMigrations: true);

        // Act
        var indexes = await db.Database
            .SqlQueryRaw<string>(
                "SELECT indexname FROM pg_indexes WHERE tablename = 'idempotency_keys' AND schemaname = 'chronith'")
            .ToListAsync();

        // Assert
        indexes.Should().Contain("ix_idempotency_lookup");
    }

    [Fact]
    public async Task CustomerIndex_UniqueEmailIndex_ExistsAfterMigration()
    {
        // Arrange
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString,
            Guid.NewGuid(),
            applyMigrations: true);

        // Act
        var indexes = await db.Database
            .SqlQueryRaw<string>(
                "SELECT indexname FROM pg_indexes WHERE tablename = 'customers' AND schemaname = 'chronith'")
            .ToListAsync();

        // Assert
        indexes.Should().Contain("ix_customers_email");
    }
}
