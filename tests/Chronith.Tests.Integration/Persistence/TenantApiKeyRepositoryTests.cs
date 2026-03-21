using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class TenantApiKeyRepositoryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Insert_AndQuery_ByKeyHash()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var (_, keyHash) = Chronith.Domain.Models.TenantApiKey.GenerateKey();

        var entity = new TenantApiKeyEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            KeyHash = keyHash,
            Description = "Test API key",
            Scopes = ["bookings:read"],
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.TenantApiKeys.Add(entity);
        await db.SaveChangesAsync();

        var found = await db.TenantApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash);

        found.Should().NotBeNull();
        found!.Id.Should().Be(entity.Id);
        found.TenantId.Should().Be(tenantId);
        found.KeyHash.Should().Be(keyHash);
        found.Description.Should().Be("Test API key");
        found.Scopes.Should().BeEquivalentTo(["bookings:read"]);
        found.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Revoke_SetsIsRevoked_True()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var (_, keyHash) = Chronith.Domain.Models.TenantApiKey.GenerateKey();

        var entity = new TenantApiKeyEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            KeyHash = keyHash,
            Description = "Revocable key",
            Scopes = ["bookings:write"],
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.TenantApiKeys.Add(entity);
        await db.SaveChangesAsync();

        // Revoke it
        entity.IsRevoked = true;
        await db.SaveChangesAsync();

        // Re-query
        var found = await db.TenantApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash);

        found.Should().NotBeNull();
        found!.IsRevoked.Should().BeTrue();
    }
}
