using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using FluentAssertions;

namespace Chronith.Tests.Functional.Auth;

[Collection("Functional")]
public class RegisterTests(FunctionalTestFixture fixture)
{
    [Fact]
    public async Task Register_WithValidRequest_Returns201WithTokens()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            tenantName = "Acme Corp",
            tenantSlug = "acme-" + Guid.NewGuid().ToString("N")[..8],
            timeZoneId = "UTC",
            email = $"owner-{Guid.NewGuid():N}@example.com",
            password = "Password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AuthTokenDto>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_WithDuplicateSlug_Returns409()
    {
        var slug = "dup-" + Guid.NewGuid().ToString("N")[..8];
        var client = fixture.CreateAnonymousClient();

        await client.PostAsJsonAsync("/auth/register", new
        {
            tenantName = "First", tenantSlug = slug, timeZoneId = "UTC",
            email = $"{Guid.NewGuid():N}@example.com", password = "Password123"
        });

        var second = await client.PostAsJsonAsync("/auth/register", new
        {
            tenantName = "Second", tenantSlug = slug, timeZoneId = "UTC",
            email = $"{Guid.NewGuid():N}@example.com", password = "Password123"
        });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns400()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            tenantName = "X", tenantSlug = "x-" + Guid.NewGuid().ToString("N")[..8],
            timeZoneId = "UTC", email = "a@b.com", password = "short"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
