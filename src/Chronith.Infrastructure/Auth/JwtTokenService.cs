using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
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
            new Claim("email", customer.Email),
            new Claim("tenantSlug", tenantSlug),
            new Claim("purpose", "magic-link-verify"),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Guid ValidateMagicLinkToken(string token, string tenantSlug)
    {
        try
        {
            var signingKey = GetPrimarySigningKey();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out _);

            var purpose = principal.FindFirstValue("purpose");
            if (purpose != "magic-link-verify")
                throw new UnauthorizedException("Invalid token purpose.");

            var tokenTenantSlug = principal.FindFirstValue("tenantSlug");
            if (tokenTenantSlug != tenantSlug)
                throw new UnauthorizedException("Tenant slug mismatch.");

            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (sub is null || !Guid.TryParse(sub, out var customerId))
                throw new UnauthorizedException("Invalid token subject.");

            return customerId;
        }
        catch (UnauthorizedException)
        {
            throw;
        }
        catch (SecurityTokenException)
        {
            throw new UnauthorizedException("Invalid or expired magic link token.");
        }
        catch (ArgumentException)
        {
            throw new UnauthorizedException("Invalid or expired magic link token.");
        }
    }

    public string CreateEmailVerificationToken(Guid userId)
    {
        var signingKey = GetPrimarySigningKey();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("purpose", "email-verify"),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Guid? ValidateEmailVerificationToken(string token)
    {
        try
        {
            var signingKey = GetPrimarySigningKey();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out _);

            var purpose = principal.FindFirstValue("purpose");
            if (purpose != "email-verify")
                return null;

            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (sub is null || !Guid.TryParse(sub, out var userId))
                return null;

            return userId;
        }
        catch
        {
            return null;
        }
    }
}
