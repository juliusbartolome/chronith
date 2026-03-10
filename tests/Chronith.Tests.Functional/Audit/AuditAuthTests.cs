using System.Net;
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

    // GET /audit/entries — Admin only

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetAuditEntries_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);

        var response = await client.GetAsync("/v1/audit/entries");

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetAuditEntries_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/v1/audit/entries");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /audit/entries/{id} — Admin only

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetAuditEntryById_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);

        var response = await client.GetAsync($"/v1/audit/entries/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetAuditEntryById_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync($"/v1/audit/entries/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
