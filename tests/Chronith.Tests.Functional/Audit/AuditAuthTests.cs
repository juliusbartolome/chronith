using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Audit;

[Collection("Functional")]
public sealed class AuditAuthTests(FunctionalTestFixture fixture)
{
    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    // GET /audit — Admin only

    [Fact]
    public async Task GetAuditEntries_AsAdmin_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync("/v1/audit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetAuditEntries_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);

        var response = await client.GetAsync("/v1/audit");

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetAuditEntries_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/v1/audit");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /audit/{id} — Admin only

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetAuditEntryById_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);

        var response = await client.GetAsync($"/v1/audit/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetAuditEntryById_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync($"/v1/audit/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // API key scope tests

    [Fact]
    public async Task GetAuditEntries_WithApiKey_WithAuditReadScope_Returns200()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.AuditRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.GetAsync("/v1/audit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
