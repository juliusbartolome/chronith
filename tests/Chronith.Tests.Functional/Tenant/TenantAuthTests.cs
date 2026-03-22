using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Tenant;

[Collection("Functional")]
public sealed class TenantAuthTests(FunctionalTestFixture fixture)
{
    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    // GET /tenant — Admin → 200; Staff/Customer/PaymentSvc → 403; anon → 401

    [Fact]
    public async Task GetTenant_Admin_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync("/v1/tenant");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetTenant_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync("/v1/tenant");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetTenant_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/v1/tenant");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTenant_WithApiKey_WithTenantReadScope_Returns200()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TenantRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.GetAsync("/v1/tenant");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
