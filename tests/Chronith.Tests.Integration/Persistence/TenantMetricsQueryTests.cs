using Chronith.Domain.Enums;
using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Infrastructure.Persistence.Repositories;
using Chronith.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class TenantMetricsQueryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task GetMetricsAsync_ReturnsCorrectBookingCountsByStatus()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var bookingTypeId = await SeedBookingTypeAsync(db, tenantId);

        // Seed 3 confirmed + 1 cancelled bookings
        for (int i = 0; i < 3; i++)
            await SeedBookingAsync(db, tenantId, bookingTypeId, BookingStatus.Confirmed);
        await SeedBookingAsync(db, tenantId, bookingTypeId, BookingStatus.Cancelled);

        var monthStartUtc = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var repo = new BookingRepository(db);
        var metrics = await repo.GetMetricsAsync(tenantId, monthStartUtc);

        metrics.Total.Should().Be(4);
        metrics.ByStatus.Should().ContainKey(BookingStatus.Confirmed)
            .WhoseValue.Should().Be(3);
        metrics.ByStatus.Should().ContainKey(BookingStatus.Cancelled)
            .WhoseValue.Should().Be(1);
    }

    [Fact]
    public async Task GetTypeMetricsAsync_IgnoresQueryFilters_CountsArchivedTypes()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        // 2 active + 1 soft-deleted booking type
        await SeedBookingTypeAsync(db, tenantId, isDeleted: false);
        await SeedBookingTypeAsync(db, tenantId, isDeleted: false);
        await SeedBookingTypeAsync(db, tenantId, isDeleted: true);

        var repo = new BookingTypeRepository(db);
        var metrics = await repo.GetTypeMetricsAsync(tenantId);

        metrics.Active.Should().Be(2);
        metrics.Archived.Should().Be(1);
    }

    [Fact]
    public async Task GetDeliveryMetricsAsync_ScopedToTenant_DoesNotCountOtherTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var dbA = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantA, applyMigrations: true);
        await using var dbB = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantB, applyMigrations: false);

        // TenantA: 5 delivered, 2 failed
        var (_, webhookA) = await SeedWebhookAsync(dbA, tenantA);
        for (int i = 0; i < 5; i++)
            await SeedOutboxEntryAsync(dbA, tenantA, webhookA, OutboxStatus.Delivered);
        for (int i = 0; i < 2; i++)
            await SeedOutboxEntryAsync(dbA, tenantA, webhookA, OutboxStatus.Failed);

        // TenantB: 10 delivered
        var (_, webhookB) = await SeedWebhookAsync(dbB, tenantB);
        for (int i = 0; i < 10; i++)
            await SeedOutboxEntryAsync(dbB, tenantB, webhookB, OutboxStatus.Delivered);

        var repoA = new WebhookOutboxRepository(dbA);
        var metrics = await repoA.GetDeliveryMetricsAsync(tenantA);

        metrics.Delivered.Should().Be(5);
        metrics.Failed.Should().Be(2);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static async Task<Guid> SeedBookingTypeAsync(
        Chronith.Infrastructure.Persistence.ChronithDbContext db,
        Guid tenantId,
        bool isDeleted = false)
    {
        var id = Guid.NewGuid();
        db.BookingTypes.Add(new BookingTypeEntity
        {
            Id = id,
            TenantId = tenantId,
            Slug = $"type-{id:N}",
            Name = "Test Type",
            Capacity = 1,
            PaymentMode = PaymentMode.Manual,
            Kind = BookingKind.TimeSlot,
            IsDeleted = isDeleted
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> SeedBookingAsync(
        Chronith.Infrastructure.Persistence.ChronithDbContext db,
        Guid tenantId,
        Guid bookingTypeId,
        BookingStatus status)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Bookings.Add(new BookingEntity
        {
            Id = id,
            TenantId = tenantId,
            BookingTypeId = bookingTypeId,
            Start = now,
            End = now.AddHours(1),
            Status = status,
            CustomerId = $"cust-{id:N}",
            CustomerEmail = "cust@example.com",
            IsDeleted = false
        });
        await db.SaveChangesAsync();
        return id;
    }

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
            PaymentMode = PaymentMode.Manual,
            Kind = BookingKind.TimeSlot,
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

    private static async Task SeedOutboxEntryAsync(
        Chronith.Infrastructure.Persistence.ChronithDbContext db,
        Guid tenantId,
        Guid webhookId,
        OutboxStatus status)
    {
        db.WebhookOutboxEntries.Add(new WebhookOutboxEntryEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WebhookId = webhookId,
            BookingId = Guid.NewGuid(),
            EventType = "booking.confirmed",
            Payload = "{}",
            Status = status,
            AttemptCount = status == OutboxStatus.Failed ? 6 : 1,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
