using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.BookingTypes;

[Collection("Functional")]
public sealed class BookingTypeEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BaseUrl = "/v1/booking-types";

    [Fact]
    public async Task CreateBookingType_AsAdmin_Returns201WithBody()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);

        var client = fixture.CreateClient("TenantAdmin");
        var slug = $"endpoint-test-{Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            slug,
            name = "Endpoint Test Type",
            isTimeSlot = true,
            capacity = 3,
            durationMinutes = 30,
            bufferBeforeMinutes = 0,
            bufferAfterMinutes = 0,
            availabilityWindows = new[]
            {
                new { dayOfWeek = 1, startTime = "08:00:00", endTime = "18:00:00" }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<BookingTypeDto>();
        body.Should().NotBeNull();
        body!.Slug.Should().Be(slug);
        body.Capacity.Should().Be(3);
        body.DurationMinutes.Should().Be(30);
    }

    [Fact]
    public async Task GetBookingType_AsAdmin_ReturnsCorrectSlug()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var slug = $"get-test-{Guid.NewGuid():N}";
        await SeedData.SeedBookingTypeAsync(db, slug);

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync($"{BaseUrl}/{slug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BookingTypeDto>();
        body!.Slug.Should().Be(slug);
    }

    [Fact]
    public async Task ListBookingTypes_AsAdmin_ReturnsCollection()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, $"list-test-{Guid.NewGuid():N}");

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<BookingTypeDto>>();
        body.Should().NotBeNull();
        body!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateBookingType_AsAdmin_Returns200WithUpdatedName()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var slug = $"update-test-{Guid.NewGuid():N}";
        await SeedData.SeedBookingTypeAsync(db, slug);

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.PutAsJsonAsync($"{BaseUrl}/{slug}", new
        {
            name = "Updated Name",
            capacity = 10,
            durationMinutes = 45,
            bufferBeforeMinutes = 0,
            bufferAfterMinutes = 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BookingTypeDto>();
        body!.Name.Should().Be("Updated Name");
        body.Capacity.Should().Be(10);
    }

    [Fact]
    public async Task DeleteBookingType_AsAdmin_Returns204()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var slug = $"delete-test-{Guid.NewGuid():N}";
        await SeedData.SeedBookingTypeAsync(db, slug);

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.DeleteAsync($"{BaseUrl}/{slug}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetBookingType_NotFound_Returns404()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync($"{BaseUrl}/does-not-exist-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
