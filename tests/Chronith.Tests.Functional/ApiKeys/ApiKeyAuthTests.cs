using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Functional.ApiKeys;

[Collection("Functional")]
public sealed class ApiKeyAuthTests(FunctionalTestFixture fixture)
{
    private const string ApiKeysUrl = "/v1/tenant/api-keys";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    // GET /v1/tenant/api-keys — TenantAdmin → 200; TenantStaff, Customer, TenantPaymentService → 403
    [Theory]
    [InlineData("TenantAdmin", HttpStatusCode.OK)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task ListApiKeys_ReturnsExpectedStatus(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync(ApiKeysUrl);
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ListApiKeys_Anonymous_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync(ApiKeysUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListApiKeys_WithApiKey_WithTenantReadScope_Returns200()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");

        var createResp = await adminClient.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"tenant-read-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TenantRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.GetAsync(ApiKeysUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListApiKeys_WithApiKey_WithoutTenantReadScope_Returns403()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");

        var createResp = await adminClient.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"bookings-read-only-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.GetAsync(ApiKeysUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // POST /v1/tenant/api-keys — TenantAdmin → 201; TenantStaff, Customer, TenantPaymentService → 403
    [Theory]
    [InlineData("TenantAdmin", HttpStatusCode.Created)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task CreateApiKey_ReturnsExpectedStatus(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"auth-test-key-{role.ToLower()}-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsRead }
        });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task CreateApiKey_Anonymous_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"anon-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsRead }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateApiKey_WithApiKey_Returns401()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");

        // Create a key with TenantRead scope
        var createResp = await adminClient.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"bootstrap-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TenantRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        // API keys cannot create other API keys — Bearer only scheme
        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"key-created-by-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TenantRead }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // DELETE /v1/tenant/api-keys/{id} — TenantStaff, Customer, TenantPaymentService → 403
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task RevokeApiKey_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.DeleteAsync($"{ApiKeysUrl}/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task RevokeApiKey_Anonymous_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var response = await client.DeleteAsync($"{ApiKeysUrl}/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeApiKey_WithApiKey_Returns401()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");

        // Create a key to use as the requester and another as the target
        var bootstrapResp = await adminClient.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"revoke-attempt-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TenantRead }
        });
        bootstrapResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var bootstrapKey = await bootstrapResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var targetResp = await adminClient.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"target-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsRead }
        });
        targetResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var targetKey = await targetResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        // API keys cannot revoke other API keys — Bearer only scheme
        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", bootstrapKey!.RawKey);

        var response = await apiKeyClient.DeleteAsync($"{ApiKeysUrl}/{targetKey!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
