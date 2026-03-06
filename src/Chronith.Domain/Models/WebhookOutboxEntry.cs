using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;

namespace Chronith.Domain.Models;

public sealed class WebhookOutboxEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }

    /// <summary>
    /// Set for TenantWebhook entries; null for CustomerCallback entries.
    /// </summary>
    public Guid? WebhookId { get; init; }

    /// <summary>
    /// Set for CustomerCallback entries; null for TenantWebhook entries.
    /// </summary>
    public Guid? BookingTypeId { get; init; }

    public Guid BookingId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public OutboxCategory Category { get; init; } = OutboxCategory.TenantWebhook;
    public OutboxStatus Status { get; private set; } = OutboxStatus.Pending;
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? RetryRequestedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Back-off schedule: 30s, 2m, 10m, 1h, 4h
    private static readonly TimeSpan[] BackOffSchedule =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(4)
    ];

    /// <summary>
    /// Returns the back-off delay for the given attempt number (1-indexed).
    /// Attempt 1 → 30s, 2 → 2m, 3 → 10m, 4 → 1h, 5 → 4h.
    /// </summary>
    public static TimeSpan GetBackOffDelay(int attempt) => BackOffSchedule[attempt - 1];

    public const int MaxAttempts = 6;

    public void RecordSuccess(DateTimeOffset now)
    {
        if (Status == OutboxStatus.Failed)
            throw new InvalidOperationException("Cannot deliver a permanently failed outbox entry.");
        Status = OutboxStatus.Delivered;
        DeliveredAt = now;
        LastAttemptAt = now;
    }

    public void RecordFailure(DateTimeOffset now)
    {
        AttemptCount++;
        LastAttemptAt = now;

        if (AttemptCount >= MaxAttempts)
        {
            Status = OutboxStatus.Failed;
            NextRetryAt = null;
        }
        else
        {
            NextRetryAt = now.Add(GetBackOffDelay(AttemptCount));
        }
    }

    /// <summary>
    /// Resets a Failed entry back to Pending for manual retry.
    /// Throws <see cref="InvalidStateTransitionException"/> if the entry is not in Failed status.
    /// </summary>
    public void ResetForRetry()
    {
        if (Status != OutboxStatus.Failed)
            throw new InvalidStateTransitionException(
                $"Cannot retry a delivery with status '{Status}'. Only Failed entries can be retried.");

        Status = OutboxStatus.Pending;
        AttemptCount = 0;
        NextRetryAt = DateTimeOffset.UtcNow;
        RetryRequestedAt = DateTimeOffset.UtcNow;
    }
}
