namespace Chronith.Application.DTOs;

public sealed record ApiKeyDto(
    Guid Id,
    string Description,
    IReadOnlyList<string> Scopes,
    bool IsRevoked,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);
