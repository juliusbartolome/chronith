using System.Net;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.CustomerAuth;

[Collection("Functional")]
public sealed class CustomerRefreshTests(FunctionalTestFixture fixture)
{
    private const string TenantSlug = "cust-refresh";
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000003");

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db, id: TenantId, slug: TenantSlug);
        await SeedData.SeedTenantAuthConfigAsync(db, tenantId: TenantId);
    }

    [Fact]
    public async Task Refresh_WithValidToken_Returns200WithNewTokens()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var email = $"ref-{Guid.NewGuid():N}@example.com";

        var reg = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/register", new
        {
            email, password = "Password123!", name = "Refresh Test"
        });
        var tokens = await reg.ReadFromApiJsonAsync<CustomerAuthTokenDto>();

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/refresh", new
        {
            refreshToken = tokens!.RefreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTokens = await response.ReadFromApiJsonAsync<CustomerAuthTokenDto>();
        newTokens!.AccessToken.Should().NotBeNullOrWhiteSpace();
        newTokens.RefreshToken.Should().NotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithUsedToken_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var email = $"refr-{Guid.NewGuid():N}@example.com";

        var reg = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/register", new
        {
            email, password = "Password123!", name = "Refresh Used"
        });
        var tokens = await reg.ReadFromApiJsonAsync<CustomerAuthTokenDto>();

        // Use it once
        await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/refresh", new
        {
            refreshToken = tokens!.RefreshToken
        });

        // Use it again — should fail
        var second = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/refresh", new
        {
            refreshToken = tokens.RefreshToken
        });

        second.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/refresh", new
        {
            refreshToken = "not-a-real-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
