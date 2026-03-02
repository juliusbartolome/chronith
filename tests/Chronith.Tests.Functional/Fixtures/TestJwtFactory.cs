using FastEndpoints.Security;

namespace Chronith.Tests.Functional.Fixtures;

public static class TestJwtFactory
{
    public static string CreateToken(string role, string userId, Guid? tenantId = null)
    {
        var tid = (tenantId ?? TestConstants.TenantId).ToString();
        return JwtBearer.CreateToken(o =>
        {
            o.SigningKey = TestConstants.JwtSigningKey;
            o.ExpireAt = DateTime.UtcNow.AddHours(1);
            o.User.Claims.Add(("tenant_id", tid));
            o.User.Claims.Add(("sub", userId));
            o.User.Roles.Add(role);
        });
    }
}
