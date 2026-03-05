using Chronith.Domain.Enums;
using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class CustomerCallbackTests(PostgresFixture postgres)
{
    [Fact]
    public async Task BookingType_CanPersist_CustomerCallbackUrl()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var entity = new BookingTypeEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = "test-callback-type",
            Name = "Test Callback Type",
            Kind = BookingKind.TimeSlot,
            Capacity = 5,
            PaymentMode = PaymentMode.Manual,
            IsDeleted = false,
            DurationMinutes = 60,
            BufferBeforeMinutes = 0,
            BufferAfterMinutes = 0,
            CustomerCallbackUrl = "https://customer.example.com/callback",
            CustomerCallbackSecret = "abc123def456abc123def456abc123def456abc123def456abc123def456abc1"
        };
        db.BookingTypes.Add(entity);
        await db.SaveChangesAsync();

        var loaded = await db.BookingTypes
            .IgnoreQueryFilters()
            .FirstAsync(bt => bt.Id == entity.Id);

        loaded.CustomerCallbackUrl.Should().Be("https://customer.example.com/callback");
        loaded.CustomerCallbackSecret.Should().Be("abc123def456abc123def456abc123def456abc123def456abc123def456abc1");
    }

    [Fact]
    public async Task CustomerCallbackOutboxEntry_CanPersist_WithNullWebhookId()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var bookingTypeId = Guid.NewGuid();
        var entry = new WebhookOutboxEntryEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WebhookId = null,
            BookingTypeId = bookingTypeId,
            BookingId = Guid.NewGuid(),
            EventType = "customer.booking.confirmed",
            Payload = "{}",
            Status = OutboxStatus.Pending,
            AttemptCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            Category = 1  // OutboxCategory.CustomerCallback
        };
        db.WebhookOutboxEntries.Add(entry);
        await db.SaveChangesAsync();

        var loaded = await db.WebhookOutboxEntries
            .AsNoTracking()
            .FirstAsync(e => e.Id == entry.Id);

        loaded.WebhookId.Should().BeNull();
        loaded.BookingTypeId.Should().Be(bookingTypeId);
        loaded.Category.Should().Be(1);
    }

    [Fact]
    public async Task ExistingOutboxEntries_DefaultCategory_IsZero_AfterMigration()
    {
        // Arrange — insert an entry without explicitly setting Category
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var webhookId = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO chronith.webhook_outbox_entries 
            ("Id","TenantId","WebhookId","BookingId","EventType","Payload","Status","AttemptCount","CreatedAt")
            VALUES ({0},{1},{2},{3},{4},{5},{6},{7},{8})
            """,
            Guid.NewGuid(), tenantId, webhookId, Guid.NewGuid(),
            "booking.confirmed", "{}", "Pending", 0, DateTimeOffset.UtcNow);

        // Assert — Category defaults to 0
        var entry = await db.WebhookOutboxEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .FirstAsync();

        entry.Category.Should().Be(0);
    }
}
