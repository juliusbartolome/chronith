namespace Chronith.Application.DTOs;

public sealed record ApiKeyDto(
    Guid Id,
    string Description,
    string Role,
    bool IsRevoked,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);
