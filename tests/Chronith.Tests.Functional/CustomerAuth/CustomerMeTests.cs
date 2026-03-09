using System.Net;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.CustomerAuth;

[Collection("Functional")]
public sealed class CustomerMeTests(FunctionalTestFixture fixture)
{
    private const string TenantSlug = "cust-me";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db, slug: TenantSlug);
        await SeedData.SeedTenantAuthConfigAsync(db);
    }

    [Fact]
    public async Task GetMe_WithValidToken_Returns200WithProfile()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var email = $"me-{Guid.NewGuid():N}@example.com";

        // Register a customer to get a valid token
        var reg = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/register", new
        {
            email, password = "Password123!", name = "Me Test"
        });
        var tokens = await reg.Content.ReadFromJsonAsync<CustomerAuthTokenDto>();

        // Use the access token for the /me endpoint
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var response = await client.GetAsync($"/v1/public/{TenantSlug}/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerDto>();
        body!.Email.Should().Be(email);
        body.Name.Should().Be("Me Test");
    }

    [Fact]
    public async Task UpdateMe_WithValidData_Returns200WithUpdatedProfile()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var email = $"upd-{Guid.NewGuid():N}@example.com";

        var reg = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/register", new
        {
            email, password = "Password123!", name = "Original Name"
        });
        var tokens = await reg.Content.ReadFromJsonAsync<CustomerAuthTokenDto>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var response = await client.PutAsJsonAsync($"/v1/public/{TenantSlug}/auth/me", new
        {
            name = "Updated Name",
            phone = "+639171234567"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerDto>();
        body!.Name.Should().Be("Updated Name");
        body.Phone.Should().Be("+639171234567");
    }

    [Fact]
    public async Task GetMe_WithSeededCustomer_Returns200()
    {
        await EnsureSeedAsync();
        var customerId = Guid.NewGuid();
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedCustomerAsync(db, id: customerId, email: $"seeded-{customerId:N}@example.com");

        var client = fixture.CreateClientWithCustomerToken(customerId.ToString());

        var response = await client.GetAsync($"/v1/public/{TenantSlug}/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerDto>();
        body!.Id.Should().Be(customerId);
    }
}
