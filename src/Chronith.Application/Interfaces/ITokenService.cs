using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ITokenService
{
    /// <summary>Creates a 15-minute JWT access token for the given user.</summary>
    string CreateAccessToken(TenantUser user);

    /// <summary>
    /// Creates a new opaque refresh token.
    /// Returns (rawToken, tokenHash) — store the hash, return the raw token to the client.
    /// </summary>
    (string RawToken, string TokenHash) CreateRefreshToken();
}
