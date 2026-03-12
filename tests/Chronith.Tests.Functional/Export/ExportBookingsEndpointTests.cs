using System.Net;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using FluentAssertions;
using Xunit;

namespace Chronith.Tests.Functional.Export;

[Collection("Functional")]
public sealed class ExportBookingsEndpointTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "export-test-type";

    private async Task EnsureSeedAsync()
    {
        using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, slug: BookingTypeSlug);
    }

    [Fact]
    public async Task ExportBookings_CsvFormat_Returns200WithCsvContentType()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync(
            "/v1/bookings/export?format=csv&from=2020-01-01T00:00:00Z&to=2030-01-01T00:00:00Z");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        response.Content.Headers.ContentDisposition.FileName.Should().StartWith("bookings-");
    }

    [Fact]
    public async Task ExportBookings_PdfFormat_Returns200WithPdfContentType()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync(
            "/v1/bookings/export?format=pdf&from=2020-01-01T00:00:00Z&to=2030-01-01T00:00:00Z");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var body = await response.Content.ReadAsByteArrayAsync();
        body.Should().StartWith(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF header
    }

    [Fact]
    public async Task ExportBookings_StaffRole_Returns403()
    {
        var client = fixture.CreateClient("TenantStaff");
        var response = await client.GetAsync("/v1/bookings/export?format=csv");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExportBookings_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/v1/bookings/export?format=csv");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
