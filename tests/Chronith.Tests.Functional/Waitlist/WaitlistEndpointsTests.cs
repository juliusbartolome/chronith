using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Functional.Waitlist;

[Collection("Functional")]
public sealed class WaitlistEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "waitlist-test-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    [Fact]
    public async Task JoinWaitlist_AsCustomer_Returns201()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var start = DateTimeOffset.UtcNow.AddDays(20);
        var response = await client.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/waitlist", new
        {
            desiredStart = start,
            desiredEnd = start.AddHours(1)
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var entry = await response.ReadFromApiJsonAsync<WaitlistEntryDto>();
        entry.Should().NotBeNull();
        entry!.Status.Should().Be(WaitlistStatus.Waiting);
        entry.CustomerId.Should().Be(TestConstants.CustomerUserId);
    }

    [Fact]
    public async Task ListWaitlist_AsAdmin_ReturnsEntries()
    {
        await EnsureSeedAsync();

        // Join as customer first
        var customerClient = fixture.CreateClient("Customer");
        var start = DateTimeOffset.UtcNow.AddDays(21);
        var joinResp = await customerClient.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/waitlist", new
        {
            desiredStart = start,
            desiredEnd = start.AddHours(1)
        });
        joinResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // List as admin
        var adminClient = fixture.CreateClient("TenantAdmin");
        var from = Uri.EscapeDataString(start.AddHours(-1).ToString("o"));
        var to = Uri.EscapeDataString(start.AddHours(2).ToString("o"));
        var listResp = await adminClient.GetAsync(
            $"/v1/booking-types/{BookingTypeSlug}/waitlist?from={from}&to={to}");

        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var entries = await listResp.ReadFromApiJsonAsync<List<WaitlistEntryDto>>();
        entries.Should().NotBeNull();
        entries!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RemoveFromWaitlist_AsCustomer_Returns204()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        // Join first
        var start = DateTimeOffset.UtcNow.AddDays(22);
        var joinResp = await client.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/waitlist", new
        {
            desiredStart = start,
            desiredEnd = start.AddHours(1)
        });
        joinResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var entry = await joinResp.ReadFromApiJsonAsync<WaitlistEntryDto>();

        // Remove
        var deleteResp = await client.DeleteAsync($"/v1/waitlist/{entry!.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcceptWaitlistOffer_AsCustomer_Returns200()
    {
        await EnsureSeedAsync();

        // Seed a waitlist entry directly in Offered status
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var btId = (await db.BookingTypes.FirstAsync(bt => bt.Slug == BookingTypeSlug)).Id;

        var start = DateTimeOffset.UtcNow.AddDays(23);
        var entryId = await SeedData.SeedWaitlistEntryAsync(
            db, btId, start, start.AddHours(1),
            WaitlistStatus.Offered,
            customerId: TestConstants.CustomerUserId,
            offeredAt: DateTimeOffset.UtcNow,
            expiresAt: DateTimeOffset.UtcNow.AddHours(1));

        // Accept the offer as customer
        var client = fixture.CreateClient("Customer");
        var acceptResp = await client.PostAsJsonAsync($"/v1/waitlist/{entryId}/accept", new { });

        acceptResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var accepted = await acceptResp.ReadFromApiJsonAsync<WaitlistEntryDto>();
        accepted.Should().NotBeNull();
        accepted!.Status.Should().Be(WaitlistStatus.Converted);
    }
}
