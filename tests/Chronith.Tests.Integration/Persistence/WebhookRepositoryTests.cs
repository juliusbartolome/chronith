using Chronith.Application.Options;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Infrastructure.Persistence.Repositories;
using Chronith.Infrastructure.Security;
using Chronith.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class WebhookRepositoryTests(PostgresFixture postgres)
{
    private static readonly string TestKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private static WebhookRepository CreateRepo(
        Chronith.Infrastructure.Persistence.ChronithDbContext db)
    {
        var encryption = new EncryptionService(Options.Create(new EncryptionOptions
        {
            EncryptionKey = TestKey
        }));
        return new WebhookRepository(db, encryption, NullLogger<WebhookRepository>.Instance);
    }

    /// <summary>Seeds a BookingType row to satisfy the FK constraint on webhooks.</summary>
    private static async Task<Guid> SeedBookingTypeAsync(
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
        await db.SaveChangesAsync();
        return bookingTypeId;
    }

    [Fact]
    public async Task AddAsync_SecretIsEncryptedAtRest()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);
        var bookingTypeId = await SeedBookingTypeAsync(db, tenantId);

        var repo = CreateRepo(db);
        var webhook = Webhook.Create(tenantId, bookingTypeId, "https://example.com/hook", "super_secret_signing_key");

        await repo.AddAsync(webhook);
        await db.SaveChangesAsync();

        // Read raw entity — secret must NOT be plaintext
        var rawEntity = await db.Webhooks
            .IgnoreQueryFilters()
            .FirstAsync(w => w.Id == webhook.Id);
        rawEntity.Secret.Should().NotContain("super_secret_signing_key"); // encrypted at rest
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDecryptedSecret()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);
        var bookingTypeId = await SeedBookingTypeAsync(db, tenantId);

        var repo = CreateRepo(db);
        var webhook = Webhook.Create(tenantId, bookingTypeId, "https://example.com/hook", "my_signing_secret");

        await repo.AddAsync(webhook);
        await db.SaveChangesAsync();

        var found = await repo.GetByIdAsync(tenantId, webhook.Id);

        found.Should().NotBeNull();
        found!.Secret.Should().Be("my_signing_secret"); // decrypted
    }

    [Fact]
    public async Task ListAsync_ReturnsDecryptedSecrets()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);
        var bookingTypeId = await SeedBookingTypeAsync(db, tenantId);

        var repo = CreateRepo(db);
        var webhook1 = Webhook.Create(tenantId, bookingTypeId, "https://a.com/hook", "secret_one");
        var webhook2 = Webhook.Create(tenantId, bookingTypeId, "https://b.com/hook", "secret_two");

        await repo.AddAsync(webhook1);
        await repo.AddAsync(webhook2);
        await db.SaveChangesAsync();

        var list = await repo.ListAsync(tenantId, bookingTypeId);

        list.Should().HaveCount(2);
        list.Should().Contain(w => w.Secret == "secret_one");
        list.Should().Contain(w => w.Secret == "secret_two");
    }

    [Fact]
    public async Task GetByIdCrossTenantAsync_ReturnsDecryptedSecret()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);
        var bookingTypeId = await SeedBookingTypeAsync(db, tenantId);

        var repo = CreateRepo(db);
        var webhook = Webhook.Create(tenantId, bookingTypeId, "https://example.com/hook", "cross_tenant_secret");

        await repo.AddAsync(webhook);
        await db.SaveChangesAsync();

        var found = await repo.GetByIdCrossTenantAsync(webhook.Id);

        found.Should().NotBeNull();
        found!.Secret.Should().Be("cross_tenant_secret"); // decrypted
    }
}
