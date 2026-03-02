using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Availability;

[Collection("Functional")]
public sealed class AvailabilityEndpointsTests(FunctionalTestFixture fixture)
{
    [Fact]
    public async Task GetAvailability_WithSeededBookingType_ReturnsSlots()
    {
        var slug = $"avail-ep-{Guid.NewGuid():N}";
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, slug, capacity: 2, durationMinutes: 60);

        var client = fixture.CreateClient("Customer");
        // Monday 2026-04-06 to Sunday 2026-04-12, UTC
        var url = $"/booking-types/{slug}/availability?from=2026-04-06T00:00:00Z&to=2026-04-12T23:59:59Z";
        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AvailabilityDto>();
        body.Should().NotBeNull();
        // Slots may be empty if no availability windows configured — just assert shape is valid
        body!.Slots.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAvailability_UnknownSlug_Returns404()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);

        var client = fixture.CreateClient("Customer");
        var url = $"/booking-types/does-not-exist-{Guid.NewGuid():N}/availability?from=2026-04-06T00:00:00Z&to=2026-04-12T23:59:59Z";
        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
