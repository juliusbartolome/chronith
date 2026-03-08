using System.Net;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Analytics;

[Collection("Functional")]
public sealed class AnalyticsAuthTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "analytics-auth-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    private static string BuildQueryString()
    {
        var from = Uri.EscapeDataString("2026-04-01T00:00:00Z");
        var to = Uri.EscapeDataString("2026-05-01T00:00:00Z");
        return $"?from={from}&to={to}";
    }

    // GET /analytics/bookings — Admin only

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetBookingAnalytics_NonAdmin_ReturnsForbidden(
        string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync($"/analytics/bookings{BuildQueryString()}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetBookingAnalytics_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/analytics/bookings{BuildQueryString()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /analytics/revenue — Admin only

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetRevenueAnalytics_NonAdmin_ReturnsForbidden(
        string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync($"/analytics/revenue{BuildQueryString()}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetRevenueAnalytics_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/analytics/revenue{BuildQueryString()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /analytics/utilization — Admin only

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetUtilizationAnalytics_NonAdmin_ReturnsForbidden(
        string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync($"/analytics/utilization{BuildQueryString()}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetUtilizationAnalytics_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/analytics/utilization{BuildQueryString()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
