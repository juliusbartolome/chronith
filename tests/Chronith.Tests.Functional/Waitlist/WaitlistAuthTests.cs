using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Waitlist;

[Collection("Functional")]
public sealed class WaitlistAuthTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "waitlist-auth-type";

    private async Task<Guid> EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        return await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    [Fact]
    public async Task ListWaitlist_WithApiKey_WithReadScope_Returns200()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"read-scope-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();
        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("o"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(30).ToString("o"));
        var response = await apiKeyClient.GetAsync($"/v1/booking-types/{BookingTypeSlug}/waitlist?from={from}&to={to}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListWaitlist_WithApiKey_WithoutReadScope_Returns403()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"wrong-scope-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsWrite }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();
        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("o"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(30).ToString("o"));
        var response = await apiKeyClient.GetAsync($"/v1/booking-types/{BookingTypeSlug}/waitlist?from={from}&to={to}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveFromWaitlist_WithApiKey_WithCancelScope_Returns204()
    {
        await EnsureSeedAsync();

        // Join as customer first to get an entry to remove
        var customerClient = fixture.CreateClient("Customer");
        var start = DateTimeOffset.UtcNow.AddDays(25);
        var joinResp = await customerClient.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/waitlist", new
        {
            desiredStart = start,
            desiredEnd = start.AddHours(1)
        });
        joinResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var entry = await joinResp.ReadFromApiJsonAsync<WaitlistEntryDto>();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"cancel-scope-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsCancel }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();
        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.DeleteAsync($"/v1/waitlist/{entry!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveFromWaitlist_WithApiKey_WithoutCancelScope_Returns403()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"wrong-scope-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();
        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.DeleteAsync($"/v1/waitlist/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
