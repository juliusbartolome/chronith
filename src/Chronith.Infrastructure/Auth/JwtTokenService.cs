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

    public string CreateAccessToken(TenantUser user)
    {
        var signingKey = configuration["Jwt:SigningKey"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("email", user.Email),
            new Claim("chronith_role", user.Role.ToString()),
            new Claim(ClaimTypes.Role, user.AuthorizationRole),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateCustomerAccessToken(Customer customer)
    {
        var signingKey = configuration["Jwt:SigningKey"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new Claim("tenant_id", customer.TenantId.ToString()),
            new Claim("customer_id", customer.Id.ToString()),
            new Claim("email", customer.Email),
            new Claim(ClaimTypes.Role, "Customer"),
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
}
