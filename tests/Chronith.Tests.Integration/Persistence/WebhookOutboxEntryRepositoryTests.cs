using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Infrastructure.Persistence.Repositories;
using Chronith.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class WebhookOutboxEntryRepositoryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Insert_AndQuery_ByStatusAndNextRetryAt()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var entry = new WebhookOutboxEntryEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WebhookId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            EventType = "booking.confirmed",
            Payload = "{}",
            Status = OutboxStatus.Pending,
            AttemptCount = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.WebhookOutboxEntries.Add(entry);
        await db.SaveChangesAsync();

        var found = await db.WebhookOutboxEntries
            .Where(e => e.Status == OutboxStatus.Pending && e.NextRetryAt == null)
            .FirstOrDefaultAsync();

        found.Should().NotBeNull();
        found!.Id.Should().Be(entry.Id);
    }

    [Fact]
    public async Task ListByWebhookAsync_ReturnsPagedResultsInDescendingCreatedAtOrder()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var (bookingTypeId, webhookId) = await SeedWebhookAsync(db, tenantId);

        for (int i = 0; i < 25; i++)
        {
            db.WebhookOutboxEntries.Add(MakeEntry(tenantId, webhookId,
                createdAt: DateTimeOffset.UtcNow.AddMinutes(-i)));
        }
        await db.SaveChangesAsync();

        var repo = new WebhookOutboxRepository(db);
        var (items, total) = await repo.ListByWebhookAsync(webhookId, page: 1, pageSize: 20);

        total.Should().Be(25);
        items.Should().HaveCount(20);
        items.Should().BeInDescendingOrder(e => e.CreatedAt);
    }

    [Fact]
    public async Task ListByWebhookAsync_DoesNotReturnEntriesFromOtherTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var dbA = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantA, applyMigrations: true);
        await using var dbB = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantB, applyMigrations: false);

        var (_, webhookA) = await SeedWebhookAsync(dbA, tenantA);
        var (_, webhookB) = await SeedWebhookAsync(dbB, tenantB);

        dbA.WebhookOutboxEntries.Add(MakeEntry(tenantA, webhookA));
        await dbA.SaveChangesAsync();

        dbB.WebhookOutboxEntries.Add(MakeEntry(tenantB, webhookB));
        await dbB.SaveChangesAsync();

        // Query as tenant A — should only see tenantA's entry
        var repoA = new WebhookOutboxRepository(dbA);
        var (items, total) = await repoA.ListByWebhookAsync(webhookA, page: 1, pageSize: 20);

        total.Should().Be(1);
        items.Should().AllSatisfy(e => e.WebhookId.Should().Be(webhookA));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenDeliveryBelongsToDifferentTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var dbA = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantA, applyMigrations: true);
        await using var dbB = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantB, applyMigrations: false);

        var (_, webhookA) = await SeedWebhookAsync(dbA, tenantA);
        var entryId = Guid.NewGuid();
        dbA.WebhookOutboxEntries.Add(MakeEntry(tenantA, webhookA, id: entryId));
        await dbA.SaveChangesAsync();

        // Query as tenant B — should get null because webhook belongs to tenantA
        var repoB = new WebhookOutboxRepository(dbB);
        var result = await repoB.GetByIdAsync(entryId);

        result.Should().BeNull();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static async Task<(Guid BookingTypeId, Guid WebhookId)> SeedWebhookAsync(
        Chronith.Infrastructure.Persistence.ChronithDbContext db, Guid tenantId)
    {
        var bookingTypeId = Guid.NewGuid();
        db.BookingTypes.Add(new BookingTypeEntity
        {
            Id = bookingTypeId,
            TenantId = tenantId,
            Slug = $"type-{bookingTypeId:N}",
            Name = "Test Type",
            Capacity = 1,
            PaymentMode = Domain.Enums.PaymentMode.Manual,
            Kind = Domain.Enums.BookingKind.TimeSlot,
        });

        var webhookId = Guid.NewGuid();
        db.Webhooks.Add(new WebhookEntity
        {
            Id = webhookId,
            TenantId = tenantId,
            BookingTypeId = bookingTypeId,
            Url = "https://example.com/hook",
            Secret = "secret",
        });

        await db.SaveChangesAsync();
        return (bookingTypeId, webhookId);
    }

    private static WebhookOutboxEntryEntity MakeEntry(
        Guid tenantId, Guid webhookId,
        Guid? id = null,
        DateTimeOffset? createdAt = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TenantId = tenantId,
        WebhookId = webhookId,
        BookingId = Guid.NewGuid(),
        EventType = "booking.confirmed",
        Payload = "{}",
        Status = OutboxStatus.Pending,
        AttemptCount = 0,
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
    };
}
