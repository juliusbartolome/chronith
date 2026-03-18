using Chronith.Application.Options;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Repositories;
using Chronith.Infrastructure.Security;
using Chronith.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class TenantPaymentConfigRepositoryTests(PostgresFixture postgres)
{
    private static readonly string TestKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private static TenantPaymentConfigRepository CreateRepo(
        Chronith.Infrastructure.Persistence.ChronithDbContext db)
    {
        var encryption = new EncryptionService(Options.Create(new EncryptionOptions
        {
            EncryptionKey = TestKey
        }));
        return new TenantPaymentConfigRepository(db, encryption);
    }

    [Fact]
    public async Task AddAsync_GetByIdAsync_ReturnsDecryptedConfig()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = CreateRepo(db);
        var config = TenantPaymentConfig.Create(
            tenantId, "PayMongo", "Dev Key", """{"SecretKey":"sk_test_abc"}""", null, null);

        await repo.AddAsync(config);
        await db.SaveChangesAsync();

        var found = await repo.GetByIdAsync(config.Id);

        found.Should().NotBeNull();
        found!.TenantId.Should().Be(tenantId);
        found.ProviderName.Should().Be("PayMongo");
        found.Label.Should().Be("Dev Key");
        found.Settings.Should().Contain("sk_test_abc"); // decrypted
        found.IsActive.Should().BeFalse();
        found.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveByProviderNameAsync_WhenInactive_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = CreateRepo(db);
        var config = TenantPaymentConfig.Create(
            tenantId, "PayMongo", "Inactive", """{"SecretKey":"sk"}""", null, null);
        // IsActive starts false for PayMongo

        await repo.AddAsync(config);
        await db.SaveChangesAsync();

        var found = await repo.GetActiveByProviderNameAsync(tenantId, "PayMongo");

        found.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ChangesLabelAndSettings()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = CreateRepo(db);
        var config = TenantPaymentConfig.Create(
            tenantId, "PayMongo", "Old Label", """{"SecretKey":"sk_old"}""", null, null);
        await repo.AddAsync(config);
        await db.SaveChangesAsync();

        config.UpdateDetails("New Label", """{"SecretKey":"sk_new"}""", "note", null);
        await repo.UpdateAsync(config);
        await db.SaveChangesAsync();

        var found = await repo.GetByIdAsync(config.Id);
        found!.Label.Should().Be("New Label");
        found.Settings.Should().Contain("sk_new");
        found.PublicNote.Should().Be("note");
    }

    [Fact]
    public async Task DeactivateAllByProviderNameAsync_SetsIsActiveFalse()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = CreateRepo(db);
        var config = TenantPaymentConfig.Create(
            tenantId, "Manual", "GCash", "{}", "Pay via GCash", null);
        // Manual starts IsActive = true
        await repo.AddAsync(config);
        await db.SaveChangesAsync();

        await repo.DeactivateAllByProviderNameAsync(tenantId, "Manual");

        var found = await repo.GetActiveByProviderNameAsync(tenantId, "Manual");
        found.Should().BeNull();
    }

    [Fact]
    public async Task SoftDeleteAsync_MakesGetByIdReturnNull()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = CreateRepo(db);
        var config = TenantPaymentConfig.Create(
            tenantId, "PayMongo", "To Delete", "{}", null, null);
        await repo.AddAsync(config);
        await db.SaveChangesAsync();

        await repo.SoftDeleteAsync(config.Id);

        var found = await repo.GetByIdAsync(config.Id);
        found.Should().BeNull();
    }

    [Fact]
    public async Task ListByTenantAsync_ExcludesSoftDeleted()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = CreateRepo(db);
        var visible = TenantPaymentConfig.Create(tenantId, "Manual", "Cash", "{}", null, null);
        var deleted = TenantPaymentConfig.Create(tenantId, "Manual", "OldGCash", "{}", null, null);

        await repo.AddAsync(visible);
        await repo.AddAsync(deleted);
        await db.SaveChangesAsync();

        await repo.SoftDeleteAsync(deleted.Id);

        var list = await repo.ListByTenantAsync(tenantId);
        list.Should().HaveCount(1);
        list[0].Label.Should().Be("Cash");
    }

    [Fact]
    public async Task ListActiveByTenantAsync_ReturnsOnlyActive()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = CreateRepo(db);
        var active = TenantPaymentConfig.Create(tenantId, "Manual", "GCash", "{}", "QR", null);
        var inactive = TenantPaymentConfig.Create(tenantId, "PayMongo", "Dev", """{"SecretKey":"sk"}""", null, null);

        await repo.AddAsync(active);
        await repo.AddAsync(inactive);
        await db.SaveChangesAsync();

        var list = await repo.ListActiveByTenantAsync(tenantId);
        list.Should().HaveCount(1);
        list[0].ProviderName.Should().Be("Manual");
    }

    [Fact]
    public async Task AddAsync_SettingsEncryptedAtRest()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = CreateRepo(db);
        var config = TenantPaymentConfig.Create(
            tenantId, "PayMongo", "Encryption Check",
            """{"SecretKey":"super_secret_key"}""", null, null);
        await repo.AddAsync(config);
        await db.SaveChangesAsync();

        // Read raw entity to verify encryption
        var rawEntity = await db.TenantPaymentConfigs
            .FirstAsync(c => c.Id == config.Id);
        rawEntity.Settings.Should().NotContain("super_secret_key"); // encrypted at rest

        // But repository returns decrypted
        var found = await repo.GetByIdAsync(config.Id);
        found!.Settings.Should().Contain("super_secret_key");
    }
}
