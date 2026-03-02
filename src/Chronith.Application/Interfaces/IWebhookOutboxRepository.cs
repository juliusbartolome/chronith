using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IWebhookOutboxRepository
{
    Task AddRangeAsync(IEnumerable<WebhookOutboxEntry> entries, CancellationToken ct);
    Task<IReadOnlyList<PendingOutboxEntry>> GetPendingAsync(int batchSize, CancellationToken ct);
    Task MarkDeliveredAsync(Guid entryId, DateTimeOffset now, CancellationToken ct);
    Task MarkFailedAttemptAsync(Guid entryId, int newAttemptCount, DateTimeOffset? nextRetryAt, bool isFinalFailure, CancellationToken ct);
}
