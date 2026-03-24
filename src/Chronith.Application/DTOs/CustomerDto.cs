namespace Chronith.Application.DTOs;

public sealed record CustomerDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Mobile,
    string AuthProvider,
    bool IsEmailVerified,
    DateTimeOffset CreatedAt);
