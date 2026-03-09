using System.Net;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.CustomerAuth;

[Collection("Functional")]
public sealed class CustomerBookingsTests(FunctionalTestFixture fixture)
{
    private const string TenantSlug = "cust-bookings";
    private const string BookingTypeSlug = "cust-book-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db, slug: TenantSlug);
        await SeedData.SeedTenantAuthConfigAsync(db);
    }

    [Fact]
    public async Task GetMyBookings_WithValidToken_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var email = $"bk-{Guid.NewGuid():N}@example.com";

        var reg = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/register", new
        {
            email, password = "Password123!", name = "Booking Test"
        });
        var tokens = await reg.Content.ReadFromJsonAsync<CustomerAuthTokenDto>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var response = await client.GetAsync($"/v1/public/{TenantSlug}/my-bookings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMyBookingDetail_WithNonExistentBooking_Returns404()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var email = $"bd-{Guid.NewGuid():N}@example.com";

        var reg = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/register", new
        {
            email, password = "Password123!", name = "Detail Test"
        });
        var tokens = await reg.Content.ReadFromJsonAsync<CustomerAuthTokenDto>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var response = await client.GetAsync($"/v1/public/{TenantSlug}/my-bookings/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
