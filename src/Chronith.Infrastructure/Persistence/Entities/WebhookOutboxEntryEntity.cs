using Chronith.Domain.Enums;

namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class WebhookOutboxEntryEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid WebhookId { get; set; }
    public Guid BookingId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public OutboxStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
