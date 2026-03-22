using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.TimeBlocks;

[Collection("Functional")]
public sealed class TimeBlocksAuthTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "timeblocks-auth-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    [Fact]
    public async Task ListTimeBlocks_WithApiKey_WithAvailabilityReadScope_Returns200()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.AvailabilityRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("o"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(7).ToString("o"));
        var response = await apiKeyClient.GetAsync($"/v1/time-blocks?from={from}&to={to}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListTimeBlocks_WithApiKey_WithoutAvailabilityReadScope_Returns403()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TimeBlocksWrite }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("o"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(7).ToString("o"));
        var response = await apiKeyClient.GetAsync($"/v1/time-blocks?from={from}&to={to}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateTimeBlock_WithApiKey_WithTimeBlocksWriteScope_Returns201()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TimeBlocksWrite }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var start = DateTimeOffset.UtcNow.AddDays(40);
        var response = await apiKeyClient.PostAsJsonAsync("/v1/time-blocks", new
        {
            start,
            end = start.AddHours(2),
            reason = "API key block"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateTimeBlock_WithApiKey_WithoutTimeBlocksWriteScope_Returns403()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.AvailabilityRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var start = DateTimeOffset.UtcNow.AddDays(41);
        var response = await apiKeyClient.PostAsJsonAsync("/v1/time-blocks", new
        {
            start,
            end = start.AddHours(2),
            reason = "Should fail"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
