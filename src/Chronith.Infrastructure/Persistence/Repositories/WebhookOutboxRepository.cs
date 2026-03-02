using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class WebhookOutboxRepository(ChronithDbContext db) : IWebhookOutboxRepository
{
    public Task AddRangeAsync(IEnumerable<WebhookOutboxEntry> entries, CancellationToken ct)
    {
        var entities = entries.Select(MapToEntity).ToList();
        db.WebhookOutboxEntries.AddRange(entities);
        // Note: SaveChanges is called by the caller's unit of work
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<PendingOutboxEntry>> GetPendingAsync(int batchSize, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.WebhookOutboxEntries
            .AsNoTracking()
            .Where(e => e.Status == OutboxStatus.Pending
                     && (e.NextRetryAt == null || e.NextRetryAt <= now))
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .Select(e => new PendingOutboxEntry(e.Id, e.WebhookId, e.EventType, e.Payload, e.AttemptCount))
            .ToListAsync(ct);
    }

    public async Task MarkDeliveredAsync(Guid entryId, DateTimeOffset now, CancellationToken ct)
    {
        var entity = await db.WebhookOutboxEntries.FindAsync([entryId], ct)
            ?? throw new InvalidOperationException($"WebhookOutboxEntry {entryId} not found");

        entity.Status = OutboxStatus.Delivered;
        entity.DeliveredAt = now;
        entity.LastAttemptAt = now;

        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAttemptAsync(
        Guid entryId, int newAttemptCount, DateTimeOffset? nextRetryAt, bool isFinalFailure, CancellationToken ct)
    {
        var entity = await db.WebhookOutboxEntries.FindAsync([entryId], ct)
            ?? throw new InvalidOperationException($"WebhookOutboxEntry {entryId} not found");

        entity.AttemptCount = newAttemptCount;
        entity.NextRetryAt = nextRetryAt;
        entity.LastAttemptAt = DateTimeOffset.UtcNow;
        entity.Status = isFinalFailure ? OutboxStatus.Failed : OutboxStatus.Pending;

        await db.SaveChangesAsync(ct);
    }

    private static WebhookOutboxEntryEntity MapToEntity(WebhookOutboxEntry d) => new()
    {
        Id = d.Id,
        TenantId = d.TenantId,
        WebhookId = d.WebhookId,
        BookingId = d.BookingId,
        EventType = d.EventType,
        Payload = d.Payload,
        Status = d.Status,
        AttemptCount = d.AttemptCount,
        NextRetryAt = d.NextRetryAt,
        LastAttemptAt = d.LastAttemptAt,
        DeliveredAt = d.DeliveredAt,
        CreatedAt = d.CreatedAt,
    };
}
