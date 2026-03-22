using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Models;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Functional.Auth;

[Collection("Functional")]
public class MeTests(FunctionalTestFixture fixture)
{
    private async Task<(HttpClient client, string accessToken)> RegisterAndLoginAsync()
    {
        var client = fixture.CreateAnonymousClient();
        var slug = "me-" + Guid.NewGuid().ToString("N")[..8];
        var email = $"me-{Guid.NewGuid():N}@example.com";

        var reg = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            tenantName = "Me Test", tenantSlug = slug, timeZoneId = "UTC",
            email, password = "Password123"
        });
        var tokens = await reg.ReadFromApiJsonAsync<AuthTokenDto>();
        return (client, tokens!.AccessToken);
    }

    [Fact]
    public async Task GetMe_WithValidJwt_Returns200WithProfile()
    {
        var (client, token) = await RegisterAndLoginAsync();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadFromApiJsonAsync<UserProfileDto>();
        body!.Email.Should().NotBeNullOrWhiteSpace();
        body.Role.Should().Be("Owner");
    }

    [Fact]
    public async Task GetMe_WithNoAuth_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithApiKey_Returns401()
    {
        // API keys must not reach /auth/me — it is Bearer-only.
        // Create a key with tenant:read scope via admin, then try to GET /auth/me using it.
        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"me-apikey-get-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TenantRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.GetAsync("/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PatchMe_WithApiKey_Returns401()
    {
        // API keys must not reach PATCH /auth/me — it is Bearer-only.
        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"me-apikey-patch-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TenantWrite }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.PatchAsJsonAsync("/v1/auth/me", new { email = "hacker@evil.com" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PatchMe_UpdatesEmail()
    {
        var (client, token) = await RegisterAndLoginAsync();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var newEmail = $"updated-{Guid.NewGuid():N}@example.com";

        var response = await client.PatchAsJsonAsync("/v1/auth/me", new { email = newEmail });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var getMe = await client.GetAsync("/v1/auth/me");
        var body = await getMe.ReadFromApiJsonAsync<UserProfileDto>();
        body!.Email.Should().Be(newEmail);
    }
}
