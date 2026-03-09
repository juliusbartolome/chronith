using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Chronith.Tests.Functional.Fixtures;

public static class TestJwtFactory
{
    public static string CreateToken(string role, string userId, Guid? tenantId = null)
    {
        var tid = (tenantId ?? TestConstants.TenantId).ToString();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("tenant_id", tid),
            new Claim("sub", userId),
            new Claim("role", role),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string CreateCustomerToken(string customerId, Guid? tenantId = null)
    {
        var tid = (tenantId ?? TestConstants.TenantId).ToString();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("tenant_id", tid),
            new Claim("sub", customerId),
            new Claim("customer_id", customerId),
            new Claim("email", "customer@test.com"),
            new Claim("role", "Customer"),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
