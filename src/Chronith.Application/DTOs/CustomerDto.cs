namespace Chronith.Application.DTOs;

public sealed record CustomerDto(
    Guid Id,
    string Email,
    string Name,
    string? Phone,
    string AuthProvider,
    bool IsEmailVerified,
    DateTimeOffset CreatedAt);
