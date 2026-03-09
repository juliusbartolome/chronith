using System.Net;
using System.Net.Http.Json;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Recurring;

[Collection("Functional")]
public sealed class RecurringAuthTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "recurring-auth-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    // POST /booking-types/{slug}/recurring — Admin, Staff, Customer → allowed; anon → 401
    [Fact]
    public async Task CreateRecurrenceRule_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/recurring", new
        {
            frequency = "Daily",
            interval = 1,
            seriesStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /recurring — Admin, Staff → allowed; Customer → 403; anon → 401
    [Fact]
    public async Task ListRecurrenceRules_AsCustomer_Returns403()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var response = await client.GetAsync("/v1/recurring");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListRecurrenceRules_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/v1/recurring");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /recurring/{id} — Admin, Staff, Customer → allowed; anon → 401
    [Fact]
    public async Task GetRecurrenceRule_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/v1/recurring/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // PUT /recurring/{id} — Admin, Staff → allowed; Customer → 403; anon → 401
    [Fact]
    public async Task UpdateRecurrenceRule_AsCustomer_Returns403()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var response = await client.PutAsJsonAsync($"/v1/recurring/{Guid.NewGuid()}", new
        {
            frequency = "Daily",
            interval = 1,
            seriesStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateRecurrenceRule_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PutAsJsonAsync($"/v1/recurring/{Guid.NewGuid()}", new
        {
            frequency = "Daily",
            interval = 1,
            seriesStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // DELETE /recurring/{id} — Admin, Staff, Customer → allowed; anon → 401
    [Fact]
    public async Task CancelRecurrenceRule_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.DeleteAsync($"/v1/recurring/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /recurring/{id}/occurrences — Admin, Staff, Customer → allowed; anon → 401
    [Fact]
    public async Task GetOccurrences_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        var to = from.AddDays(30);
        var response = await client.GetAsync(
            $"/v1/recurring/{Guid.NewGuid()}/occurrences?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
