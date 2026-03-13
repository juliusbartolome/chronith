namespace Chronith.Client.Models;

public sealed record WebhookDto(
    Guid Id,
    Guid TenantId,
    string Url,
    IReadOnlyList<string> Events,
    bool IsActive,
    DateTimeOffset CreatedAt
);
