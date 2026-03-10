using System.Net;
using Chronith.Infrastructure.Persistence.Entities;
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
}
