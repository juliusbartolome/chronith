using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Functional.Recurring;

[Collection("Functional")]
public sealed class RecurringEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "recurring-endpoints-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    [Fact]
    public async Task CreateRecurrenceRule_AsAdmin_Returns201()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/recurring", new
        {
            frequency = "Weekly",
            interval = 1,
            daysOfWeek = new[] { 1, 3, 5 },
            seriesStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"),
            maxOccurrences = 10
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var rule = await response.Content.ReadFromJsonAsync<RecurrenceRuleDto>();
        rule.Should().NotBeNull();
        rule!.Id.Should().NotBeEmpty();
        rule.Frequency.Should().Be(RecurrenceFrequency.Weekly);
        rule.Interval.Should().Be(1);
        rule.MaxOccurrences.Should().Be(10);
    }

    [Fact]
    public async Task CreateRecurrenceRule_AsCustomer_Returns201()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var response = await client.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/recurring", new
        {
            frequency = "Daily",
            interval = 2,
            seriesStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"),
            seriesEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ListRecurrenceRules_AsAdmin_ReturnsRules()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create a rule first
        await client.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/recurring", new
        {
            frequency = "Monthly",
            interval = 1,
            seriesStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd")
        });

        var listResp = await client.GetAsync("/v1/recurring");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rules = await listResp.Content.ReadFromJsonAsync<List<RecurrenceRuleDto>>();
        rules.Should().NotBeNull();
        rules!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRecurrenceRule_AsStaff_Returns200()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");

        // Create
        var createResp = await adminClient.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/recurring", new
        {
            frequency = "Daily",
            interval = 1,
            seriesStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"),
            maxOccurrences = 5
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<RecurrenceRuleDto>();

        // Get as TenantStaff
        var staffClient = fixture.CreateClient("TenantStaff");
        var getResp = await staffClient.GetAsync($"/v1/recurring/{created!.Id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rule = await getResp.Content.ReadFromJsonAsync<RecurrenceRuleDto>();
        rule.Should().NotBeNull();
        rule!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task UpdateRecurrenceRule_AsAdmin_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create
        var createResp = await client.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/recurring", new
        {
            frequency = "Daily",
            interval = 1,
            seriesStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd")
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<RecurrenceRuleDto>();

        // Update
        var updateResp = await client.PutAsJsonAsync($"/v1/recurring/{created!.Id}", new
        {
            frequency = "Weekly",
            interval = 2,
            daysOfWeek = new[] { 1, 3 },
            seriesStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"),
            maxOccurrences = 20
        });

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResp.Content.ReadFromJsonAsync<RecurrenceRuleDto>();
        updated.Should().NotBeNull();
        updated!.Frequency.Should().Be(RecurrenceFrequency.Weekly);
        updated.Interval.Should().Be(2);
        updated.MaxOccurrences.Should().Be(20);
    }

    [Fact]
    public async Task CancelRecurrenceRule_AsCustomer_Returns204()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        // Create
        var createResp = await client.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/recurring", new
        {
            frequency = "Daily",
            interval = 1,
            seriesStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd")
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<RecurrenceRuleDto>();

        // Cancel (DELETE)
        var deleteResp = await client.DeleteAsync($"/v1/recurring/{created!.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetRecurrenceOccurrences_AsCustomer_ReturnsDateList()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var createResp = await client.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/recurring", new
        {
            frequency = "Daily",
            interval = 1,
            seriesStart = start.ToString("yyyy-MM-dd"),
            maxOccurrences = 7
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<RecurrenceRuleDto>();

        var from = start;
        var to = start.AddDays(30);
        var customerClient = fixture.CreateClient("Customer");
        var occResp = await customerClient.GetAsync(
            $"/v1/recurring/{created!.Id}/occurrences?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        occResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var occurrences = await occResp.Content.ReadFromJsonAsync<List<string>>();
        occurrences.Should().NotBeNull();
        occurrences!.Count.Should().Be(7);
    }
}
