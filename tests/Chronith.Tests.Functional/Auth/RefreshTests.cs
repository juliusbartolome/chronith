using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using FluentAssertions;

namespace Chronith.Tests.Functional.Auth;

[Collection("Functional")]
public class RefreshTests(FunctionalTestFixture fixture)
{
    [Fact]
    public async Task Refresh_WithValidToken_Returns200WithNewTokens()
    {
        var client = fixture.CreateAnonymousClient();
        var slug = "ref-" + Guid.NewGuid().ToString("N")[..8];
        var email = $"ref-{Guid.NewGuid():N}@example.com";

        var reg = await client.PostAsJsonAsync("/auth/register", new
        {
            tenantName = "T", tenantSlug = slug, timeZoneId = "UTC",
            email, password = "Password123"
        });
        var tokens = await reg.Content.ReadFromJsonAsync<AuthTokenDto>();

        var response = await client.PostAsJsonAsync("/auth/refresh",
            new { refreshToken = tokens!.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTokens = await response.Content.ReadFromJsonAsync<AuthTokenDto>();
        newTokens!.AccessToken.Should().NotBeNullOrWhiteSpace();
        newTokens.RefreshToken.Should().NotBe(tokens.RefreshToken); // rotated
    }

    [Fact]
    public async Task Refresh_WithUsedToken_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var slug = "refr-" + Guid.NewGuid().ToString("N")[..8];
        var email = $"refr-{Guid.NewGuid():N}@example.com";

        var reg = await client.PostAsJsonAsync("/auth/register", new
        {
            tenantName = "T", tenantSlug = slug, timeZoneId = "UTC",
            email, password = "Password123"
        });
        var tokens = await reg.Content.ReadFromJsonAsync<AuthTokenDto>();

        // Use it once
        await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = tokens!.RefreshToken });

        // Use it again — should fail
        var second = await client.PostAsJsonAsync("/auth/refresh",
            new { refreshToken = tokens.RefreshToken });

        second.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/auth/refresh",
            new { refreshToken = "not-a-real-token" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
