using System.Net;
using Chronith.Application.DTOs;
using Chronith.Application.Services;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Chronith.Tests.Functional.CustomerAuth;

/// <summary>
/// Tests for the unified POST /v1/public/{slug}/auth/login endpoint when
/// provider = "oidc". Uses a custom WebApplicationFactory that replaces
/// IOidcTokenValidator with a stub so we don't need a real IdP.
/// </summary>
[Collection("Functional")]
public sealed class CustomerOidcLoginTests(FunctionalTestFixture fixture)
{
    private const string TenantSlug = "cust-oidc-login";
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000010");

    private const string FakeIdToken = "eyJhbGciOiJSUzI1NiJ9.fake.token";
    private const string OidcIssuer = "https://test.auth0.com/";
    private const string OidcClientId = "test-client-id";

    // ---------- hand-written stub (no NSubstitute needed) ----------

    /// <summary>
    /// A test double for IOidcTokenValidator that always returns a fixed result.
    /// </summary>
    private sealed class StubOidcTokenValidator(OidcValidationResult result) : IOidcTokenValidator
    {
        public Task<OidcValidationResult> ValidateAsync(
            string idToken, string issuer, string clientId, string? audience,
            CancellationToken ct = default)
            => Task.FromResult(result);
    }

    // ---------------------------------------------------------------

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db, id: TenantId, slug: TenantSlug);
        await SeedData.SeedTenantAuthConfigAsync(
            db,
            tenantId: TenantId,
            allowBuiltIn: false,
            oidcIssuer: OidcIssuer,
            oidcClientId: OidcClientId);
    }

    private HttpClient CreateOidcClient(OidcValidationResult stubResult)
    {
        var stub = new StubOidcTokenValidator(stubResult);

        var factory = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.ConfigureServices(services =>
            {
                // Replace the real OIDC validator with our stub
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IOidcTokenValidator));
                if (descriptor is not null)
                    services.Remove(descriptor);
                services.AddScoped<IOidcTokenValidator>(_ => stub);
            });
        });

        return factory.CreateClient();
    }

    [Fact]
    public async Task OidcLogin_WithValidToken_Returns200WithJwtAndCustomerDto()
    {
        await EnsureSeedAsync();

        var externalId = $"oidc|{Guid.NewGuid():N}";
        var validResult = new OidcValidationResult(
            IsValid: true,
            ExternalId: externalId,
            Email: $"{externalId}@oidc.test",
            Name: "OIDC Test User",
            Error: null);

        var client = CreateOidcClient(validResult);

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/login", new
        {
            provider = "oidc",
            token = FakeIdToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadFromApiJsonAsync<CustomerAuthTokenDto>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.Customer.Email.Should().Be($"{externalId}@oidc.test");
    }

    [Fact]
    public async Task OidcLogin_SecondCallWithSameExternalId_AutomaticallyLogsIn()
    {
        await EnsureSeedAsync();

        var externalId = $"oidc|{Guid.NewGuid():N}";
        var validResult = new OidcValidationResult(
            IsValid: true,
            ExternalId: externalId,
            Email: $"{externalId}@oidc.test",
            Name: "OIDC Repeat User",
            Error: null);

        var client = CreateOidcClient(validResult);

        // First call — auto-creates the customer
        var first = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/login", new
        {
            provider = "oidc",
            token = FakeIdToken
        });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second call — finds existing customer, returns new tokens
        var second = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/login", new
        {
            provider = "oidc",
            token = FakeIdToken
        });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await second.ReadFromApiJsonAsync<CustomerAuthTokenDto>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OidcLogin_WithInvalidToken_Returns401()
    {
        await EnsureSeedAsync();

        var invalidResult = new OidcValidationResult(
            IsValid: false,
            ExternalId: null,
            Email: null,
            Name: null,
            Error: "Signature verification failed.");

        var client = CreateOidcClient(invalidResult);

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/login", new
        {
            provider = "oidc",
            token = "invalid.token.here"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OidcLogin_WithMissingToken_Returns400()
    {
        await EnsureSeedAsync();

        // Use the real (non-stubbed) anonymous client — token validation won't be reached
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/login", new
        {
            provider = "oidc"
            // no token field
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BuiltInLogin_StillWorksAfterOidcEndpointChange()
    {
        // Regression: built-in login path must still work through the unified endpoint
        await EnsureSeedAsync();

        // Seed a separate tenant that allows built-in auth for this regression test
        const string builtInSlug = "cust-oidc-builtin-regression";
        var builtInTenantId = Guid.Parse("10000000-0000-0000-0000-000000000011");
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db, id: builtInTenantId, slug: builtInSlug);
        await SeedData.SeedTenantAuthConfigAsync(db, tenantId: builtInTenantId, allowBuiltIn: true);

        var client = fixture.CreateAnonymousClient();
        var email = $"builtin-regression-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!";

        // Register
        var reg = await client.PostAsJsonAsync($"/v1/public/{builtInSlug}/auth/register", new
        {
            email, password, name = "Regression User"
        });
        reg.StatusCode.Should().Be(HttpStatusCode.Created);

        // Login via builtin provider (default — omitting provider field)
        var login = await client.PostAsJsonAsync($"/v1/public/{builtInSlug}/auth/login", new
        {
            email, password
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await login.ReadFromApiJsonAsync<CustomerAuthTokenDto>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }
}
