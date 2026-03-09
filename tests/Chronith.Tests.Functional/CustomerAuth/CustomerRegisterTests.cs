using System.Net;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.CustomerAuth;

[Collection("Functional")]
public sealed class CustomerRegisterTests(FunctionalTestFixture fixture)
{
    private const string TenantSlug = "cust-reg";
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db, id: TenantId, slug: TenantSlug);
        await SeedData.SeedTenantAuthConfigAsync(db, tenantId: TenantId);
    }

    [Fact]
    public async Task Register_WithValidData_Returns201WithTokens()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var email = $"reg-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/register", new
        {
            email,
            password = "Password123!",
            name = "Test Customer"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadFromApiJsonAsync<CustomerAuthTokenDto>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.Customer.Email.Should().Be(email);
        body.Customer.Name.Should().Be("Test Customer");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/register", new
        {
            email,
            password = "Password123!",
            name = "First"
        });

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/register", new
        {
            email,
            password = "Password123!",
            name = "Second"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithInvalidSlug_Returns404()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/v1/public/nonexistent-slug/auth/register", new
        {
            email = "nobody@example.com",
            password = "Password123!",
            name = "Test"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
