using Chronith.Domain.Enums;

namespace Chronith.Application.DTOs;

public sealed record WebhookDeliveryDto(
    Guid Id,
    Guid WebhookId,
    Guid BookingId,
    string EventType,
    OutboxStatus Status,
    int AttemptCount,
    DateTimeOffset? NextRetryAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? RetryRequestedAt,
    DateTimeOffset CreatedAt);
