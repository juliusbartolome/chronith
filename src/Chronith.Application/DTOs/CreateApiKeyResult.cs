namespace Chronith.Application.DTOs;

// RawKey is returned once and never stored; all other fields come from the stored record
public sealed record CreateApiKeyResult(
    Guid Id,
    string RawKey,
    string Description,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt);
