using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Tenant;

[Collection("Functional")]
public sealed class TenantMetricsTests(FunctionalTestFixture fixture)
{
    private const string Slug = "metrics-bt";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    [Fact]
    public async Task GetMetrics_AsAdmin_Returns200WithCorrectShape()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync("/tenant/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TenantMetricsDto>();
        body.Should().NotBeNull();
        body!.Bookings.Should().NotBeNull();
        body.Webhooks.Should().NotBeNull();
        body.BookingTypes.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMetrics_AsStaff_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantStaff");

        var response = await client.GetAsync("/tenant/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMetrics_AsCustomer_Returns403()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var response = await client.GetAsync("/tenant/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMetrics_ReflectsSeededData()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, Slug);

        var now = DateTimeOffset.UtcNow;
        await SeedData.SeedBookingAsync(db, bookingTypeId, now, now.AddHours(1));

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync("/tenant/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TenantMetricsDto>();
        body!.Bookings.Total.Should().BeGreaterThanOrEqualTo(1);
        body.BookingTypes.Active.Should().BeGreaterThanOrEqualTo(1);
    }
}
