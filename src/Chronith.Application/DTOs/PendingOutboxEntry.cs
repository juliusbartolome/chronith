using Chronith.Domain.Enums;

namespace Chronith.Application.DTOs;

public sealed record PendingOutboxEntry(
    Guid Id,
    Guid? WebhookId,
    Guid? BookingTypeId,
    string EventType,
    string Payload,
    int AttemptCount,
    OutboxCategory Category);
