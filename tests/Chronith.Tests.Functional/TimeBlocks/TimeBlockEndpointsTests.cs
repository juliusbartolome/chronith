using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.TimeBlocks;

[Collection("Functional")]
public sealed class TimeBlockEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "timeblock-test-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    [Fact]
    public async Task CreateTimeBlock_AsAdmin_Returns201()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var start = DateTimeOffset.UtcNow.AddDays(30);
        var response = await client.PostAsJsonAsync("/v1/time-blocks", new
        {
            start,
            end = start.AddHours(2),
            reason = "Staff meeting"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var block = await response.Content.ReadFromJsonAsync<TimeBlockDto>();
        block.Should().NotBeNull();
        block!.Reason.Should().Be("Staff meeting");
        block.Start.Should().BeCloseTo(start, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ListTimeBlocks_AsAdmin_ReturnsBlocks()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create a time block first
        var start = DateTimeOffset.UtcNow.AddDays(31);
        var createResp = await client.PostAsJsonAsync("/v1/time-blocks", new
        {
            start,
            end = start.AddHours(2),
            reason = "List test block"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // List
        var from = Uri.EscapeDataString(start.AddHours(-1).ToString("o"));
        var to = Uri.EscapeDataString(start.AddHours(3).ToString("o"));
        var listResp = await client.GetAsync($"/v1/time-blocks?from={from}&to={to}");

        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var blocks = await listResp.Content.ReadFromJsonAsync<List<TimeBlockDto>>();
        blocks.Should().NotBeNull();
        blocks!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DeleteTimeBlock_AsAdmin_Returns204()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create
        var start = DateTimeOffset.UtcNow.AddDays(32);
        var createResp = await client.PostAsJsonAsync("/v1/time-blocks", new
        {
            start,
            end = start.AddHours(2),
            reason = "Delete test block"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var block = await createResp.Content.ReadFromJsonAsync<TimeBlockDto>();

        // Delete
        var deleteResp = await client.DeleteAsync($"/v1/time-blocks/{block!.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone from list
        var from = Uri.EscapeDataString(start.AddHours(-1).ToString("o"));
        var to = Uri.EscapeDataString(start.AddHours(3).ToString("o"));
        var listResp = await client.GetAsync($"/v1/time-blocks?from={from}&to={to}");
        var blocks = await listResp.Content.ReadFromJsonAsync<List<TimeBlockDto>>();
        blocks!.Should().NotContain(b => b.Id == block.Id);
    }

    [Fact]
    public async Task Availability_ExcludesTimeBlockedSlots()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");
        var customerClient = fixture.CreateClient("Customer");

        // Create a time block that covers a specific time window
        var blockDate = DateTimeOffset.UtcNow.Date.AddDays(33);
        var blockStart = new DateTimeOffset(blockDate.Year, blockDate.Month, blockDate.Day,
            10, 0, 0, TimeSpan.Zero);
        var blockEnd = blockStart.AddHours(2); // 10:00-12:00

        var createBlockResp = await adminClient.PostAsJsonAsync("/v1/time-blocks", new
        {
            start = blockStart,
            end = blockEnd,
            reason = "Blocked for availability test"
        });
        createBlockResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Query availability for the same day
        var from = Uri.EscapeDataString(blockStart.AddHours(-2).ToString("o"));
        var to = Uri.EscapeDataString(blockEnd.AddHours(4).ToString("o"));
        var availResp = await customerClient.GetAsync(
            $"/v1/booking-types/{BookingTypeSlug}/availability?from={from}&to={to}");

        availResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var availability = await availResp.Content.ReadFromJsonAsync<AvailabilityDto>();
        availability.Should().NotBeNull();

        // Verify no slots overlap with the time block
        foreach (var slot in availability!.Slots)
        {
            var overlaps = slot.Start < blockEnd && slot.End > blockStart;
            overlaps.Should().BeFalse(
                $"Slot {slot.Start}-{slot.End} should not overlap with time block {blockStart}-{blockEnd}");
        }
    }
}
