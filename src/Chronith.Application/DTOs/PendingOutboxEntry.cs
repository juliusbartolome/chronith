namespace Chronith.Application.DTOs;

public sealed record PendingOutboxEntry(
    Guid Id,
    Guid WebhookId,
    string EventType,
    string Payload,
    int AttemptCount);
