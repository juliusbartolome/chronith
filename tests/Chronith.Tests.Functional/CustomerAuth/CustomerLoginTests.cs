using System.Net;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.CustomerAuth;

[Collection("Functional")]
public sealed class CustomerLoginTests(FunctionalTestFixture fixture)
{
    private const string TenantSlug = "cust-login";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db, slug: TenantSlug);
        await SeedData.SeedTenantAuthConfigAsync(db);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithTokens()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var email = $"login-{Guid.NewGuid():N}@example.com";
        var password = "Password123!";

        await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/register", new
        {
            email, password, name = "Login Test"
        });

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/login", new
        {
            email, password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerAuthTokenDto>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.Customer.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var email = $"lp-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/register", new
        {
            email, password = "Password123!", name = "Test"
        });

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/login", new
        {
            email, password = "WrongPass1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/login", new
        {
            email = "nobody@example.com",
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
