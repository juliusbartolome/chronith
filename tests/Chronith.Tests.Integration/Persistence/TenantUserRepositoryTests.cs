using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Infrastructure.Persistence.Repositories;
using Chronith.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public class TenantUserRepositoryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AddAsync_ThenGetByEmailAsync_ReturnsSameUser()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        // Seed a tenant entity so the FK is satisfied
        db.Tenants.Add(new TenantEntity
        {
            Id = tenantId, Slug = $"t-{tenantId:N}", Name = "Test", TimeZoneId = "UTC",
            IsDeleted = false, CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var repo = new TenantUserRepository(db);
        var user = TenantUser.Create(tenantId, "test@example.com", "hash", TenantUserRole.Owner);
        await repo.AddAsync(user);
        await db.SaveChangesAsync();

        var found = await repo.GetByEmailAsync(tenantId, "test@example.com");
        found.Should().NotBeNull();
        found!.Email.Should().Be("test@example.com");
        found.Role.Should().Be(TenantUserRole.Owner);
    }

    [Fact]
    public async Task GetByEmailAsync_IsCaseInsensitive()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        db.Tenants.Add(new TenantEntity
        {
            Id = tenantId, Slug = $"t-{tenantId:N}", Name = "Test", TimeZoneId = "UTC",
            IsDeleted = false, CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var repo = new TenantUserRepository(db);
        var user = TenantUser.Create(tenantId, "user@example.com", "hash", TenantUserRole.Owner);
        await repo.AddAsync(user);
        await db.SaveChangesAsync();

        var found = await repo.GetByEmailAsync(tenantId, "USER@EXAMPLE.COM");
        found.Should().NotBeNull();
    }

    [Fact]
    public async Task ExistsByEmailAsync_WhenExists_ReturnsTrue()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        db.Tenants.Add(new TenantEntity
        {
            Id = tenantId, Slug = $"t-{tenantId:N}", Name = "Test", TimeZoneId = "UTC",
            IsDeleted = false, CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var repo = new TenantUserRepository(db);
        var user = TenantUser.Create(tenantId, "exists@example.com", "hash", TenantUserRole.Owner);
        await repo.AddAsync(user);
        await db.SaveChangesAsync();

        var exists = await repo.ExistsByEmailAsync(tenantId, "exists@example.com");
        exists.Should().BeTrue();
    }
}
