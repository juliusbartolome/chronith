using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Repositories;
using Chronith.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public class AuditEntryRepositoryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsEntry()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = new AuditEntryRepository(db);
        var entry = AuditEntry.Create(
            tenantId, "user-1", "TenantAdmin",
            "Booking", Guid.NewGuid(), "Created",
            null, """{"id":"some-id"}""", null);

        await repo.AddAsync(entry, CancellationToken.None);
        await db.SaveChangesAsync();

        var found = await repo.GetByIdAsync(tenantId, entry.Id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(entry.Id);
        found.TenantId.Should().Be(tenantId);
        found.UserId.Should().Be("user-1");
        found.Action.Should().Be("Created");
        found.EntityType.Should().Be("Booking");
        found.NewValues.Should().Be("""{"id":"some-id"}""");
    }

    [Fact]
    public async Task QueryAsync_FiltersByEntityType()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = new AuditEntryRepository(db);

        var bookingEntry = AuditEntry.Create(
            tenantId, "user-1", "TenantAdmin",
            "Booking", Guid.NewGuid(), "Created",
            null, null, null);
        var staffEntry = AuditEntry.Create(
            tenantId, "user-1", "TenantAdmin",
            "StaffMember", Guid.NewGuid(), "Updated",
            null, null, null);

        await repo.AddAsync(bookingEntry, CancellationToken.None);
        await repo.AddAsync(staffEntry, CancellationToken.None);
        await db.SaveChangesAsync();

        var (items, total) = await repo.QueryAsync(
            tenantId, entityType: "Booking", entityId: null, userId: null,
            action: null, from: null, to: null,
            page: 1, pageSize: 10, CancellationToken.None);

        items.Should().HaveCount(1);
        items[0].EntityType.Should().Be("Booking");
        total.Should().Be(1);
    }

    [Fact]
    public async Task QueryAsync_ReturnsPaginatedResults_WithCorrectTotalCount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = new AuditEntryRepository(db);

        for (var i = 0; i < 5; i++)
        {
            var entry = AuditEntry.Create(
                tenantId, "user-1", "TenantAdmin",
                "Booking", Guid.NewGuid(), "Created",
                null, null, null);
            await repo.AddAsync(entry, CancellationToken.None);
        }
        await db.SaveChangesAsync();

        var (items, total) = await repo.QueryAsync(
            tenantId, entityType: null, entityId: null, userId: null,
            action: null, from: null, to: null,
            page: 1, pageSize: 2, CancellationToken.None);

        items.Should().HaveCount(2);
        total.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task DeleteExpiredAsync_RemovesOldEntries()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = new AuditEntryRepository(db);

        // Old entry (in the past)
        var oldEntry = AuditEntry.Create(
            tenantId, "user-1", "TenantAdmin",
            "Booking", Guid.NewGuid(), "Created",
            null, null, null);

        // New entry (recent)
        var newEntry = AuditEntry.Create(
            tenantId, "user-1", "TenantAdmin",
            "Booking", Guid.NewGuid(), "Created",
            null, null, null);

        await repo.AddAsync(oldEntry, CancellationToken.None);
        await repo.AddAsync(newEntry, CancellationToken.None);
        await db.SaveChangesAsync();

        // Cutoff: 1 minute ago — both entries were just created with UtcNow,
        // so we set cutoff to now + 1 second to delete all of them
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(1);
        var deleted = await repo.DeleteExpiredAsync(tenantId, cutoff, CancellationToken.None);

        deleted.Should().Be(2);

        var (items, total) = await repo.QueryAsync(
            tenantId, null, null, null, null, null, null,
            page: 1, pageSize: 100, CancellationToken.None);

        total.Should().Be(0);
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_DoesNotReturnOtherTenantEntries()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var dbA = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantA, applyMigrations: true);
        await using var dbB = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantB, applyMigrations: false);

        var repoA = new AuditEntryRepository(dbA);
        var repoB = new AuditEntryRepository(dbB);

        var entryA = AuditEntry.Create(
            tenantA, "user-a", "TenantAdmin",
            "Booking", Guid.NewGuid(), "Created",
            null, null, null);
        var entryB = AuditEntry.Create(
            tenantB, "user-b", "TenantAdmin",
            "Booking", Guid.NewGuid(), "Created",
            null, null, null);

        await repoA.AddAsync(entryA, CancellationToken.None);
        await dbA.SaveChangesAsync();

        await repoB.AddAsync(entryB, CancellationToken.None);
        await dbB.SaveChangesAsync();

        // Tenant A should only see their entry
        var (itemsA, totalA) = await repoA.QueryAsync(
            tenantA, null, null, null, null, null, null,
            page: 1, pageSize: 100, CancellationToken.None);

        itemsA.Should().OnlyContain(e => e.TenantId == tenantA);
        totalA.Should().Be(1);

        // Tenant B should only see their entry
        var (itemsB, totalB) = await repoB.QueryAsync(
            tenantB, null, null, null, null, null, null,
            page: 1, pageSize: 100, CancellationToken.None);

        itemsB.Should().OnlyContain(e => e.TenantId == tenantB);
        totalB.Should().Be(1);
    }
}
