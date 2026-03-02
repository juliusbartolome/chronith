using System.Net;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Availability;

[Collection("Functional")]
public sealed class AvailabilityAuthTests(FunctionalTestFixture fixture)
{
    private const string Slug = "avail-auth-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, Slug);
    }

    private string Url => $"/booking-types/{Slug}/availability?from=2026-04-01T00:00:00Z&to=2026-04-07T00:00:00Z";

    // TenantAdmin, TenantStaff, Customer → 200; TenantPaymentService → 403; anon → 401
    [Theory]
    [InlineData("TenantAdmin", HttpStatusCode.OK)]
    [InlineData("TenantStaff", HttpStatusCode.OK)]
    [InlineData("Customer", HttpStatusCode.OK)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetAvailability_ReturnsExpectedStatus(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync(Url);
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetAvailability_Anonymous_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync(Url);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
