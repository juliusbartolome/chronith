using System.Net;
using Chronith.Tests.Functional.Fixtures;

namespace Chronith.Tests.Functional.Signup;

[Collection("Functional")]
public sealed class SignupAuthTests(FunctionalTestFixture fixture)
{
    [Fact]
    public async Task Signup_Anonymous_CanReachEndpoint()
    {
        var client = fixture.CreateAnonymousClient();
        var slug = "signup-auth-" + Guid.NewGuid().ToString("N")[..8];
        var email = $"signup-auth-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            tenantName = "Signup Auth Test",
            tenantSlug = slug,
            timeZoneId = "UTC",
            email,
            password = "Password123"
        });

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyEmail_Anonymous_CanReachEndpoint()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/v1/auth/verify-email", new
        {
            token = "invalid-token"
        });

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
