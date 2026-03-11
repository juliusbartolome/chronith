using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ITokenService
{
    /// <summary>Creates a 15-minute JWT access token for the given user.</summary>
    string CreateAccessToken(TenantUser user);

    /// <summary>Creates a 15-minute JWT access token for the given customer.</summary>
    string CreateCustomerAccessToken(Customer customer);

    /// <summary>
    /// Creates a new opaque refresh token.
    /// Returns (rawToken, tokenHash) — store the hash, return the raw token to the client.
    /// </summary>
    (string RawToken, string TokenHash) CreateRefreshToken();

    /// <summary>
    /// Creates a 24-hour signed JWT for magic link email verification.
    /// Claims: sub (customer ID), email, tenantSlug, purpose="magic-link-verify"
    /// </summary>
    string CreateMagicLinkToken(Customer customer, string tenantSlug);

    /// <summary>
    /// Validates a magic link token. Returns the customer ID if valid.
    /// Throws UnauthorizedException if invalid, expired, or wrong purpose/tenantSlug.
    /// </summary>
    Guid ValidateMagicLinkToken(string token, string tenantSlug);
}
