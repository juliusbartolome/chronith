using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Tenant;

[Collection("Functional")]
public sealed class TenantEndpointsTests(FunctionalTestFixture fixture)
{
    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    [Fact]
    public async Task GetTenant_AsAdmin_ReturnsCorrectShape()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync("/v1/tenant");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tenant = await response.Content.ReadFromJsonAsync<TenantDto>();
        tenant.Should().NotBeNull();
        tenant!.Id.Should().Be(TestConstants.TenantId);
        tenant.Name.Should().NotBeNullOrEmpty();
        tenant.TimeZoneId.Should().NotBeNullOrEmpty();
        tenant.Slug.Should().NotBeNullOrEmpty();
    }
}
