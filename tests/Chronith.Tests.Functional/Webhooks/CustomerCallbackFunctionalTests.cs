using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Functional.Webhooks;

[Collection("Functional")]
public sealed class CustomerCallbackFunctionalTests(FunctionalTestFixture fixture)
{
    [Fact]
    public async Task ConfirmBooking_WithCallbackUrl_WritesCustomerCallbackOutboxEntry()
    {
        // Arrange — seed tenant and booking type
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var slug = $"cb-func-{Guid.NewGuid():N}";
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, slug);

        // Update booking type to set a CustomerCallbackUrl via PUT
        var adminClient = fixture.CreateClient("TenantAdmin");
        var updateResponse = await adminClient.PutAsJsonAsync($"/v1/booking-types/{slug}", new
        {
            name = "CB Func Type",
            capacity = 5,
            durationMinutes = 60,
            bufferBeforeMinutes = 0,
            bufferAfterMinutes = 0,
            priceInCentavos = 10_000L,
            currency = "PHP",
            customerCallbackUrl = "https://customer.example.com/callback",
            availabilityWindows = Enumerable.Range(0, 7).Select(d => new
            {
                dayOfWeek = d,
                startTime = "08:00:00",
                endTime = "18:00:00"
            })
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedType = await updateResponse.ReadFromApiJsonAsync<BookingTypeDto>();
        updatedType!.CustomerCallbackUrl.Should().Be("https://customer.example.com/callback");

        // Create booking
        var customerClient = fixture.CreateClient("Customer");
        var createResp = await customerClient.PostAsJsonAsync($"/v1/booking-types/{slug}/bookings", new
        {
            startTime = "2027-08-01T09:00:00Z",
            customerEmail = $"cb-func-{Guid.NewGuid():N}@example.com"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await createResp.ReadFromApiJsonAsync<BookingDto>();
        booking.Should().NotBeNull();

        // Pay → PendingVerification
        var staffClient = fixture.CreateClient("TenantStaff");
        var payResp = await staffClient.PostAsJsonAsync($"/v1/bookings/{booking!.Id}/pay", new
        {
            bookingTypeSlug = slug
        });
        payResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Confirm → Confirmed (triggers outbox handler)
        var confirmResp = await staffClient.PostAsJsonAsync($"/v1/bookings/{booking.Id}/confirm", new
        {
            bookingTypeSlug = slug
        });
        confirmResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await confirmResp.ReadFromApiJsonAsync<BookingDto>();
        confirmed!.Status.Should().Be(Domain.Enums.BookingStatus.Confirmed);

        // Assert — CustomerCallback outbox entry was created
        await using var db2 = SeedData.CreateDbContext(fixture.Factory);
        var entries = await db2.WebhookOutboxEntries
            .AsNoTracking()
            .Where(e => e.BookingId == booking.Id)
            .ToListAsync();

        entries.Should().Contain(e =>
            e.Category == 1 &&          // OutboxCategory.CustomerCallback
            e.BookingTypeId == bookingTypeId &&
            e.EventType == "customer.booking.confirmed");
    }
}
