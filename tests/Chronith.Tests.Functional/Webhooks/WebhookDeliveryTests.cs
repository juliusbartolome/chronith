using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Functional.Webhooks;

[Collection("Functional")]
public sealed class WebhookDeliveryTests(FunctionalTestFixture fixture)
{
    // Use unique slugs per test to avoid cross-test webhook pollution
    private const string WithWebhookSlug = "webhook-delivery-with-hook";
    private const string WithoutWebhookSlug = "webhook-delivery-no-hook";

    [Fact]
    public async Task ConfirmBooking_WithWebhookSubscribed_CreatesOutboxEntry()
    {
        // Arrange: seed tenant, booking type, booking in PendingVerification, and a webhook
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, WithWebhookSlug);

        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(7).AddHours(9), TimeSpan.Zero);
        var end = start.AddHours(1);
        var bookingId = await SeedData.SeedBookingAsync(
            db,
            bookingTypeId,
            start,
            end,
            status: BookingStatus.PendingVerification,
            customerId: $"wh-delivery-cust-{Guid.NewGuid():N}");

        await SeedData.SeedWebhookAsync(db, bookingTypeId);

        // Act: confirm the booking as TenantAdmin
        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.PostAsJsonAsync($"/v1/bookings/{bookingId}/confirm", new
        {
            bookingTypeSlug = WithWebhookSlug
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await response.ReadFromApiJsonAsync<BookingDto>();
        confirmed!.Status.Should().Be(BookingStatus.Confirmed);

        // Assert: a WebhookOutboxEntry row exists for this booking (filter to webhook category only)
        await using var assertDb = SeedData.CreateDbContext(fixture.Factory);
        var entries = await assertDb.WebhookOutboxEntries
            .Where(e => e.BookingId == bookingId && e.Category == (int)OutboxCategory.TenantWebhook)
            .ToListAsync();

        entries.Should().HaveCount(1);
        entries[0].EventType.Should().Be("booking.confirmed");
        entries[0].TenantId.Should().Be(TestConstants.TenantId);
    }

    [Fact]
    public async Task ConfirmBooking_WithNoWebhooks_NoOutboxEntry()
    {
        // Arrange: seed tenant, booking type, booking in PendingVerification — no webhook
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, WithoutWebhookSlug);

        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(8).AddHours(10), TimeSpan.Zero);
        var end = start.AddHours(1);
        var bookingId = await SeedData.SeedBookingAsync(
            db,
            bookingTypeId,
            start,
            end,
            status: BookingStatus.PendingVerification,
            customerId: $"wh-no-hook-cust-{Guid.NewGuid():N}");

        // Act: confirm the booking as TenantAdmin (no webhook seeded for this booking type)
        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.PostAsJsonAsync($"/v1/bookings/{bookingId}/confirm", new
        {
            bookingTypeSlug = WithoutWebhookSlug
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await response.ReadFromApiJsonAsync<BookingDto>();
        confirmed!.Status.Should().Be(BookingStatus.Confirmed);

        // Assert: no WebhookOutboxEntry for this booking (filter to webhook category only)
        await using var assertDb = SeedData.CreateDbContext(fixture.Factory);
        var entries = await assertDb.WebhookOutboxEntries
            .Where(e => e.BookingId == bookingId && e.Category == (int)OutboxCategory.TenantWebhook)
            .ToListAsync();

        entries.Should().BeEmpty();
    }
}
