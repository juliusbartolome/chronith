using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
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
