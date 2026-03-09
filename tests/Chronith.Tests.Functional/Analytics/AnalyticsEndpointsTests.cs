using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Functional.Analytics;

[Collection("Functional")]
public sealed class AnalyticsEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "analytics-endpoints-type";

    private async Task<Guid> EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var btId = await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug,
            priceInCentavos: 10_000);

        // Seed bookings with various statuses for analytics
        var start = new DateTimeOffset(2026, 4, 7, 10, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(1);

        // 2 confirmed bookings with revenue
        await SeedData.SeedBookingAsync(db, btId, start, end,
            BookingStatus.Confirmed, "analytics-cust-1", amountInCentavos: 10_000);
        await SeedData.SeedBookingAsync(db, btId, start.AddHours(2), end.AddHours(2),
            BookingStatus.Confirmed, "analytics-cust-2", amountInCentavos: 15_000);

        // 1 cancelled booking (no revenue)
        await SeedData.SeedBookingAsync(db, btId, start.AddDays(1), end.AddDays(1),
            BookingStatus.Cancelled, "analytics-cust-3");

        // 1 pending payment (no revenue)
        await SeedData.SeedBookingAsync(db, btId, start.AddDays(2), end.AddDays(2),
            BookingStatus.PendingPayment, "analytics-cust-4", amountInCentavos: 10_000);

        return btId;
    }

    [Fact]
    public async Task GetBookingAnalytics_AsAdmin_ReturnsAnalytics()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var from = Uri.EscapeDataString("2026-04-07T00:00:00Z");
        var to = Uri.EscapeDataString("2026-04-15T00:00:00Z");
        var response = await client.GetAsync($"/v1/analytics/bookings?from={from}&to={to}&groupBy=day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var analytics = await response.ReadFromApiJsonAsync<BookingAnalyticsDto>();
        analytics.Should().NotBeNull();
        analytics!.Total.Should().BeGreaterThanOrEqualTo(4);
        analytics.ByStatus.Should().ContainKey("confirmed");
        analytics.ByBookingType.Should().Contain(bt => bt.Slug == BookingTypeSlug);
        analytics.TimeSeries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRevenueAnalytics_AsAdmin_ReturnsTotals()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var from = Uri.EscapeDataString("2026-04-07T00:00:00Z");
        var to = Uri.EscapeDataString("2026-04-15T00:00:00Z");
        var response = await client.GetAsync($"/v1/analytics/revenue?from={from}&to={to}&groupBy=day");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var analytics = await response.ReadFromApiJsonAsync<RevenueAnalyticsDto>();
        analytics.Should().NotBeNull();
        analytics!.TotalCentavos.Should().BeGreaterThanOrEqualTo(25_000);
        analytics.Currency.Should().Be("PHP");
        analytics.ByBookingType.Should().Contain(bt => bt.Slug == BookingTypeSlug);
        analytics.TimeSeries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUtilizationAnalytics_AsAdmin_ReturnsUtilization()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var from = Uri.EscapeDataString("2026-04-07T00:00:00Z");
        var to = Uri.EscapeDataString("2026-04-15T00:00:00Z");
        var response = await client.GetAsync($"/v1/analytics/utilization?from={from}&to={to}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var analytics = await response.ReadFromApiJsonAsync<UtilizationAnalyticsDto>();
        analytics.Should().NotBeNull();
        analytics!.ByBookingType.Should().Contain(bt => bt.Slug == BookingTypeSlug);
    }

    [Fact]
    public async Task GetBookingAnalytics_GroupByMonth_ReturnsMonthlyTimeSeries()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var from = Uri.EscapeDataString("2026-04-01T00:00:00Z");
        var to = Uri.EscapeDataString("2026-05-01T00:00:00Z");
        var response = await client.GetAsync($"/v1/analytics/bookings?from={from}&to={to}&groupBy=month");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var analytics = await response.ReadFromApiJsonAsync<BookingAnalyticsDto>();
        analytics.Should().NotBeNull();
        analytics!.TimeSeries.Should().Contain(ts => ts.Date == "2026-04");
    }
}
