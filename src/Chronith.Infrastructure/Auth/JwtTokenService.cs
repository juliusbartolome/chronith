using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Chronith.Infrastructure.Auth;

public sealed class JwtTokenService(IConfiguration configuration) : ITokenService
{
    private const int AccessTokenMinutes = 15;

    /// <summary>
    /// Returns the primary signing key. If <c>Jwt:SigningKeys</c> is configured, uses the first entry;
    /// otherwise falls back to the single <c>Jwt:SigningKey</c> value for backward compatibility.
    /// </summary>
    private string GetPrimarySigningKey()
    {
        var keys = configuration.GetSection("Jwt:SigningKeys").Get<string[]>();
        if (keys is { Length: > 0 })
            return keys[0];

        return configuration["Jwt:SigningKey"]!;
    }

    public string CreateAccessToken(TenantUser user)
    {
        var signingKey = GetPrimarySigningKey();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("email", user.Email),
            new Claim("chronith_role", user.Role.ToString()),
            new Claim("role", user.AuthorizationRole),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateCustomerAccessToken(Customer customer)
    {
        var signingKey = GetPrimarySigningKey();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new Claim("tenant_id", customer.TenantId.ToString()),
            new Claim("customer_id", customer.Id.ToString()),
            new Claim("email", customer.Email),
            new Claim("role", "Customer"),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string RawToken, string TokenHash) CreateRefreshToken()
    {
        var raw = Guid.NewGuid().ToString("N"); // 32-char lowercase hex, no dashes
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var hash = Convert.ToHexStringLower(hashBytes);
        return (raw, hash);
    }

    public string CreateMagicLinkToken(Customer customer, string tenantSlug)
    {
        var signingKey = GetPrimarySigningKey();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, customer.Email),
            new Claim("tenantSlug", tenantSlug),
            new Claim("purpose", "magic-link-verify"),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
