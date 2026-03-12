using System.Net;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using FluentAssertions;
using Xunit;

namespace Chronith.Tests.Functional.Export;

[Collection("Functional")]
public sealed class ExportAuditEndpointTests(FunctionalTestFixture fixture)
{
    private async Task EnsureSeedAsync()
    {
        using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    [Fact]
    public async Task ExportAudit_Returns200WithCsvContentType()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync("/v1/audit/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
    }

    [Fact]
    public async Task ExportAudit_StaffRole_Returns403()
    {
        var client = fixture.CreateClient("TenantStaff");
        var response = await client.GetAsync("/v1/audit/export");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
