using System.Net;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.TenantAuthConfig;

[Collection("Functional")]
public sealed class TenantAuthConfigTests(FunctionalTestFixture fixture)
{
    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    // PUT /tenant/auth-config — Admin
    [Fact]
    public async Task PutAuthConfig_Admin_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.PutAsJsonAsync("/v1/tenant/auth-config", new
        {
            allowBuiltInAuth = true,
            magicLinkEnabled = false,
            oidcIssuer = (string?)null,
            oidcClientId = (string?)null,
            oidcAudience = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TenantAuthConfigDto>();
        body!.AllowBuiltInAuth.Should().BeTrue();
        body.MagicLinkEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task PutAuthConfig_ThenGet_ReturnsConsistentData()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        await client.PutAsJsonAsync("/v1/tenant/auth-config", new
        {
            allowBuiltInAuth = true,
            magicLinkEnabled = true,
            oidcIssuer = "https://login.example.com",
            oidcClientId = "my-client-id",
            oidcAudience = "my-audience"
        });

        var response = await client.GetAsync("/v1/tenant/auth-config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TenantAuthConfigDto>();
        body!.AllowBuiltInAuth.Should().BeTrue();
        body.MagicLinkEnabled.Should().BeTrue();
        body.OidcIssuer.Should().Be("https://login.example.com");
        body.OidcClientId.Should().Be("my-client-id");
        body.OidcAudience.Should().Be("my-audience");
    }

    // GET /tenant/auth-config — Admin
    [Fact]
    public async Task GetAuthConfig_Admin_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync("/v1/tenant/auth-config");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Non-admin roles → 403
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task PutAuthConfig_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);

        var response = await client.PutAsJsonAsync("/v1/tenant/auth-config", new
        {
            allowBuiltInAuth = true,
            magicLinkEnabled = false
        });

        response.StatusCode.Should().Be(expected);
    }

    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetAuthConfig_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync("/v1/tenant/auth-config");
        response.StatusCode.Should().Be(expected);
    }

    // Anonymous → 401
    [Fact]
    public async Task PutAuthConfig_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PutAsJsonAsync("/v1/tenant/auth-config", new
        {
            allowBuiltInAuth = true,
            magicLinkEnabled = false
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuthConfig_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/v1/tenant/auth-config");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
