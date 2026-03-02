using Chronith.Domain.Enums;

namespace Chronith.Domain.Models;

public sealed class WebhookOutboxEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid WebhookId { get; init; }
    public Guid BookingId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public OutboxStatus Status { get; private set; } = OutboxStatus.Pending;
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Back-off schedule: 30s, 2m, 10m, 1h, 4h
    public static readonly TimeSpan[] BackOffSchedule =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(4)
    ];

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
            NextRetryAt = now.Add(BackOffSchedule[AttemptCount - 1]);
        }
    }
}
