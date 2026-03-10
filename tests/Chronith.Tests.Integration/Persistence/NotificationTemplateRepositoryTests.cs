using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Repositories;
using Chronith.Tests.Integration.Fixtures;
using FluentAssertions;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public class NotificationTemplateRepositoryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AddRangeAsync_ThenGetAllAsync_ReturnsAllTemplatesForTenant()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = new NotificationTemplateRepository(db);

        var templates = new[]
        {
            NotificationTemplate.Create(tenantId, "booking.confirmed", "email",
                body: "Your booking is confirmed.", subject: "Booking Confirmed"),
            NotificationTemplate.Create(tenantId, "booking.confirmed", "sms",
                body: "Booking confirmed.", subject: null),
        };

        await repo.AddRangeAsync(templates);
        await db.SaveChangesAsync();

        var result = await repo.GetAllAsync(tenantId);
        result.Should().HaveCount(2);
        result.Select(t => t.ChannelType).Should()
            .BeEquivalentTo(["email", "sms"]);
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsDomainModel()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = new NotificationTemplateRepository(db);
        var template = NotificationTemplate.Create(tenantId, "booking.cancelled", "email",
            body: "Your booking was cancelled.", subject: "Booking Cancelled");

        await repo.AddRangeAsync([template]);
        await db.SaveChangesAsync();

        var found = await repo.GetByIdAsync(tenantId, template.Id);
        found.Should().NotBeNull();
        found!.EventType.Should().Be("booking.cancelled");
        found.ChannelType.Should().Be("email");
        found.Subject.Should().Be("Booking Cancelled");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = new NotificationTemplateRepository(db);

        var found = await repo.GetByIdAsync(tenantId, Guid.NewGuid());
        found.Should().BeNull();
    }

    [Fact]
    public async Task GetByEventAndChannelAsync_WhenExists_ReturnsTemplate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = new NotificationTemplateRepository(db);
        var template = NotificationTemplate.Create(tenantId, "booking.reminder", "push",
            body: "Your booking is tomorrow.", subject: null);

        await repo.AddRangeAsync([template]);
        await db.SaveChangesAsync();

        var found = await repo.GetByEventAndChannelAsync(tenantId, "booking.reminder", "push");
        found.Should().NotBeNull();
        found!.Body.Should().Be("Your booking is tomorrow.");
    }

    [Fact]
    public async Task UpdateAsync_ChangesBodyAndIsActive()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = new NotificationTemplateRepository(db);
        var template = NotificationTemplate.Create(tenantId, "booking.confirmed", "email",
            body: "Original body.", subject: "Subject");

        await repo.AddRangeAsync([template]);
        await db.SaveChangesAsync();

        template.UpdateBody("Updated body.", "Updated Subject");
        template.Deactivate();
        await repo.UpdateAsync(template);
        await db.SaveChangesAsync();

        var updated = await repo.GetByIdAsync(tenantId, template.Id);
        updated.Should().NotBeNull();
        updated!.Body.Should().Be("Updated body.");
        updated.Subject.Should().Be("Updated Subject");
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteByEventTypeAsync_RemovesAllMatchingTemplates()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = new NotificationTemplateRepository(db);

        var templates = new[]
        {
            NotificationTemplate.Create(tenantId, "booking.confirmed", "email",
                body: "Body.", subject: null),
            NotificationTemplate.Create(tenantId, "booking.confirmed", "sms",
                body: "SMS.", subject: null),
            NotificationTemplate.Create(tenantId, "booking.cancelled", "email",
                body: "Cancelled.", subject: null),
        };

        await repo.AddRangeAsync(templates);
        await db.SaveChangesAsync();

        await repo.DeleteByEventTypeAsync(tenantId, "booking.confirmed");

        // Use a new db context to bypass the in-memory tracker
        await using var db2 = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: false);

        var repo2 = new NotificationTemplateRepository(db2);
        var remaining = await repo2.GetAllAsync(tenantId);
        remaining.Should().HaveCount(1);
        remaining[0].EventType.Should().Be("booking.cancelled");
    }

    [Fact]
    public async Task GetAllAsync_DoesNotReturnTemplatesForOtherTenants()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        var repo = new NotificationTemplateRepository(db);

        // Seed for target tenant
        var t1 = NotificationTemplate.Create(tenantId, "booking.confirmed", "email",
            body: "Body.", subject: null);
        await repo.AddRangeAsync([t1]);

        // Seed for other tenant — bypass global filter by using entity directly
        db.NotificationTemplates.Add(new Chronith.Infrastructure.Persistence.Entities.NotificationTemplateEntity
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            EventType = "booking.confirmed",
            ChannelType = "email",
            Body = "Other.",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();

        var result = await repo.GetAllAsync(tenantId);
        result.Should().HaveCount(1);
        result[0].TenantId.Should().Be(tenantId);
    }
}
