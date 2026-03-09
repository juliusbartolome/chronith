using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Idempotency;

[Collection("Functional")]
public sealed class IdempotencyTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "idempotency-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug, capacity: 50, durationMinutes: 60);
    }

    [Fact]
    public async Task PostWithIdempotencyKey_FirstRequest_Returns201()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");
        var idempotencyKey = Guid.NewGuid().ToString();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/booking-types/{BookingTypeSlug}/bookings")
        {
            Content = JsonContent.Create(new
            {
                startTime = "2026-07-01T09:00:00Z",
                customerEmail = $"idem-first-{Guid.NewGuid():N}@example.com"
            })
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatus.PendingPayment);
    }

    [Fact]
    public async Task PostWithSameKeyAndBody_ReturnsIdempotentReplay()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");
        var idempotencyKey = Guid.NewGuid().ToString();
        var email = $"idem-replay-{Guid.NewGuid():N}@example.com";
        var body = new { startTime = "2026-07-02T10:00:00Z", customerEmail = email };

        // First request
        var request1 = new HttpRequestMessage(HttpMethod.Post, $"/v1/booking-types/{BookingTypeSlug}/bookings")
        {
            Content = JsonContent.Create(body)
        };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);
        var response1 = await client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        var body1 = await response1.Content.ReadAsStringAsync();

        // Second request — same key, same body
        var request2 = new HttpRequestMessage(HttpMethod.Post, $"/v1/booking-types/{BookingTypeSlug}/bookings")
        {
            Content = JsonContent.Create(body)
        };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await client.SendAsync(request2);

        response2.StatusCode.Should().Be(HttpStatusCode.Created);
        var body2 = await response2.Content.ReadAsStringAsync();
        body2.Should().Be(body1);
    }

    [Fact]
    public async Task PostWithSameKeyButDifferentBody_Returns422()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");
        var idempotencyKey = Guid.NewGuid().ToString();

        // First request
        var request1 = new HttpRequestMessage(HttpMethod.Post, $"/v1/booking-types/{BookingTypeSlug}/bookings")
        {
            Content = JsonContent.Create(new
            {
                startTime = "2026-07-03T11:00:00Z",
                customerEmail = $"idem-mismatch1-{Guid.NewGuid():N}@example.com"
            })
        };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);
        var response1 = await client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second request — same key, DIFFERENT body
        var request2 = new HttpRequestMessage(HttpMethod.Post, $"/v1/booking-types/{BookingTypeSlug}/bookings")
        {
            Content = JsonContent.Create(new
            {
                startTime = "2026-07-04T12:00:00Z",
                customerEmail = $"idem-mismatch2-{Guid.NewGuid():N}@example.com"
            })
        };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await client.SendAsync(request2);

        response2.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PostWithDifferentKey_CreatesNewBooking()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        // First request with key A
        var request1 = new HttpRequestMessage(HttpMethod.Post, $"/v1/booking-types/{BookingTypeSlug}/bookings")
        {
            Content = JsonContent.Create(new
            {
                startTime = "2026-07-05T09:00:00Z",
                customerEmail = $"idem-diff-a-{Guid.NewGuid():N}@example.com"
            })
        };
        request1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response1 = await client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking1 = await response1.Content.ReadFromJsonAsync<BookingDto>();

        // Second request with key B
        var request2 = new HttpRequestMessage(HttpMethod.Post, $"/v1/booking-types/{BookingTypeSlug}/bookings")
        {
            Content = JsonContent.Create(new
            {
                startTime = "2026-07-05T10:00:00Z",
                customerEmail = $"idem-diff-b-{Guid.NewGuid():N}@example.com"
            })
        };
        request2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response2 = await client.SendAsync(request2);

        response2.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking2 = await response2.Content.ReadFromJsonAsync<BookingDto>();
        booking2!.Id.Should().NotBe(booking1!.Id);
    }

    [Fact]
    public async Task PostWithoutIdempotencyKey_ProceedsNormally()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        // No Idempotency-Key header
        var response = await client.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/bookings", new
        {
            startTime = "2026-07-06T14:00:00Z",
            customerEmail = $"idem-none-{Guid.NewGuid():N}@example.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatus.PendingPayment);
    }
}
