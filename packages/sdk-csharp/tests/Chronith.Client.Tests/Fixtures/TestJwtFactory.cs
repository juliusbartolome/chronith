using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Chronith.Client.Tests.Fixtures;

public static class TestJwtFactory
{
    public static string CreateToken(string role, string? userId = null, Guid? tenantId = null)
    {
        var uid = userId ?? RoleToUserId(role);
        var tid = (tenantId ?? TestConstants.TenantId).ToString();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("tenant_id", tid),
            new Claim("sub", uid),
            new Claim("role", role),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string RoleToUserId(string role) => role switch
    {
        "TenantAdmin"          => TestConstants.AdminUserId,
        "TenantStaff"          => TestConstants.StaffUserId,
        "Customer"             => TestConstants.CustomerUserId,
        "TenantPaymentService" => TestConstants.PaymentSvcUserId,
        _                      => "user-unknown"
    };
}
