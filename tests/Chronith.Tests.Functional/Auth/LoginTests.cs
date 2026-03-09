using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Functional.Auth;

[Collection("Functional")]
public class LoginTests(FunctionalTestFixture fixture)
{
    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithTokens()
    {
        var client = fixture.CreateAnonymousClient();
        var email = $"login-{Guid.NewGuid():N}@example.com";
        var password = "Password123";
        var slug = "login-" + Guid.NewGuid().ToString("N")[..8];

        await client.PostAsJsonAsync("/v1/auth/register", new
        {
            tenantName = "Login Test", tenantSlug = slug, timeZoneId = "UTC",
            email, password
        });

        var response = await client.PostAsJsonAsync("/v1/auth/login", new { email, password, tenantSlug = slug });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadFromApiJsonAsync<AuthTokenDto>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var slug = "lp-" + Guid.NewGuid().ToString("N")[..8];
        var email = $"lp-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/v1/auth/register", new
        {
            tenantName = "Test", tenantSlug = slug, timeZoneId = "UTC",
            email, password = "Password123"
        });

        var response = await client.PostAsJsonAsync("/v1/auth/login",
            new { email, password = "WrongPass1", tenantSlug = slug });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var slug = "lu-" + Guid.NewGuid().ToString("N")[..8];

        await client.PostAsJsonAsync("/v1/auth/register", new
        {
            tenantName = "Test", tenantSlug = slug, timeZoneId = "UTC",
            email = $"lu-{Guid.NewGuid():N}@example.com", password = "Password123"
        });

        var response = await client.PostAsJsonAsync("/v1/auth/login",
            new { email = "nobody@example.com", password = "Password123", tenantSlug = slug });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
