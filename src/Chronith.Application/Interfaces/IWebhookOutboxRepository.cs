using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IWebhookOutboxRepository
{
    Task AddRangeAsync(IEnumerable<WebhookOutboxEntry> entries, CancellationToken ct);
    Task<IReadOnlyList<PendingOutboxEntry>> GetPendingAsync(int batchSize, CancellationToken ct);
    Task<IReadOnlyList<PendingOutboxEntry>> GetPendingByCategoryAsync(
        OutboxCategory category, int batchSize, CancellationToken ct);
    Task MarkDeliveredAsync(Guid entryId, DateTimeOffset now, CancellationToken ct);
    Task MarkFailedAttemptAsync(Guid entryId, int newAttemptCount, DateTimeOffset now, DateTimeOffset? nextRetryAt, bool isFinalFailure, CancellationToken ct);

    Task<(IReadOnlyList<WebhookDeliveryDto> Items, int Total)> ListByWebhookAsync(
        Guid webhookId, int page, int pageSize, CancellationToken ct = default);

    Task<WebhookDeliveryDto?> GetByIdAsync(Guid deliveryId, CancellationToken ct = default);

    /// <summary>Used by retry command — returns tracked entity for mutation.</summary>
    Task<(Guid? WebhookId, bool CanRetry)> ResetForRetryAsync(Guid deliveryId, CancellationToken ct = default);

    Task<DeliveryMetrics> GetDeliveryMetricsAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Marks an outbox entry as Abandoned — used when a CustomerCallback URL has been
    /// removed after the entry was written.
    /// </summary>
    Task MarkAbandonedAsync(Guid entryId, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes outbox entries with a terminal status (Delivered, Failed, Abandoned)
    /// that were created before <paramref name="cutoff"/>.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
