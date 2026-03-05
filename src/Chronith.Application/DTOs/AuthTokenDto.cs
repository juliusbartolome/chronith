namespace Chronith.Application.DTOs;

public sealed record AuthTokenDto(
    string AccessToken,
    string RefreshToken,
    string TokenType = "Bearer");
