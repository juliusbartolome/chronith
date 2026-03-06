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
            .Select(e => new PendingOutboxEntry(
                e.Id,
                e.WebhookId,
                e.BookingTypeId,
                e.EventType,
                e.Payload,
                e.AttemptCount,
                (OutboxCategory)e.Category))
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
        Guid entryId, int newAttemptCount, DateTimeOffset now, DateTimeOffset? nextRetryAt, bool isFinalFailure, CancellationToken ct)
    {
        var entity = await db.WebhookOutboxEntries.FindAsync([entryId], ct)
            ?? throw new InvalidOperationException($"WebhookOutboxEntry {entryId} not found");

        entity.AttemptCount = newAttemptCount;
        entity.NextRetryAt = nextRetryAt;
        entity.LastAttemptAt = now;
        entity.Status = isFinalFailure ? OutboxStatus.Failed : OutboxStatus.Pending;

        await db.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<WebhookDeliveryDto> Items, int Total)> ListByWebhookAsync(
        Guid webhookId, int page, int pageSize, CancellationToken ct = default)
    {
        // Verify webhook belongs to current tenant (global filter on db.Webhooks enforces this)
        var webhookExists = await db.Webhooks
            .AsNoTracking()
            .AnyAsync(w => w.Id == webhookId, ct);

        if (!webhookExists)
            return ([], 0);

        var query = db.WebhookOutboxEntries
            .AsNoTracking()
            .Where(e => e.WebhookId == webhookId)
            .OrderByDescending(e => e.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new WebhookDeliveryDto(
                e.Id, e.WebhookId, e.BookingId, e.EventType, e.Status,
                e.AttemptCount, e.NextRetryAt, e.LastAttemptAt, e.DeliveredAt,
                e.RetryRequestedAt, e.CreatedAt))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<WebhookDeliveryDto?> GetByIdAsync(Guid deliveryId, CancellationToken ct = default)
    {
        // Join through Webhooks so the global TenantId filter applies
        return await db.WebhookOutboxEntries
            .AsNoTracking()
            .Where(e => e.Id == deliveryId)
            .Where(e => db.Webhooks.Any(w => w.Id == e.WebhookId))
            .Select(e => new WebhookDeliveryDto(
                e.Id, e.WebhookId, e.BookingId, e.EventType, e.Status,
                e.AttemptCount, e.NextRetryAt, e.LastAttemptAt, e.DeliveredAt,
                e.RetryRequestedAt, e.CreatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(Guid? WebhookId, bool CanRetry)> ResetForRetryAsync(
        Guid deliveryId, CancellationToken ct = default)
    {
        // Load tracked entity — join through Webhooks for tenant isolation
        var entity = await db.WebhookOutboxEntries
            .Where(e => e.Id == deliveryId)
            .Where(e => db.Webhooks.Any(w => w.Id == e.WebhookId))
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return (null, false);

        if (entity.Status != OutboxStatus.Failed)
            return (entity.WebhookId, false);

        entity.Status = OutboxStatus.Pending;
        entity.AttemptCount = 0;
        entity.NextRetryAt = DateTimeOffset.UtcNow;
        entity.RetryRequestedAt = DateTimeOffset.UtcNow;

        return (entity.WebhookId, true);
    }

    public async Task<DeliveryMetrics> GetDeliveryMetricsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var delivered = await db.WebhookOutboxEntries
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId && e.Status == OutboxStatus.Delivered, ct);

        var failed = await db.WebhookOutboxEntries
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId && e.Status == OutboxStatus.Failed, ct);

        return new DeliveryMetrics(delivered, failed);
    }

    public async Task MarkAbandonedAsync(Guid entryId, CancellationToken ct = default)
    {
        var entity = await db.WebhookOutboxEntries.FindAsync([entryId], ct)
            ?? throw new InvalidOperationException($"WebhookOutboxEntry {entryId} not found");

        entity.Status = OutboxStatus.Abandoned;
        await db.SaveChangesAsync(ct);
    }

    private static WebhookOutboxEntryEntity MapToEntity(WebhookOutboxEntry d) => new()
    {
        Id = d.Id,
        TenantId = d.TenantId,
        WebhookId = d.WebhookId,
        BookingTypeId = d.BookingTypeId,
        BookingId = d.BookingId,
        EventType = d.EventType,
        Payload = d.Payload,
        Status = d.Status,
        AttemptCount = d.AttemptCount,
        NextRetryAt = d.NextRetryAt,
        LastAttemptAt = d.LastAttemptAt,
        DeliveredAt = d.DeliveredAt,
        CreatedAt = d.CreatedAt,
        Category = (int)d.Category,
    };
}

