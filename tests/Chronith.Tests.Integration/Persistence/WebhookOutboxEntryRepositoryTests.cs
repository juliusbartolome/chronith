using Chronith.Domain.Enums;
using Chronith.Infrastructure.Persistence.Entities;
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
}
