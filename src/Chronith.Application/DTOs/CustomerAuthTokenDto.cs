namespace Chronith.Application.DTOs;

public sealed record CustomerAuthTokenDto(
    string AccessToken,
    string RefreshToken,
    CustomerDto Customer);
