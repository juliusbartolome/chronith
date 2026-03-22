using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.ApiKeys;

[Collection("Functional")]
public sealed class ApiKeyEndpointsTests(FunctionalTestFixture fixture)
{
    private const string ApiKeysUrl = "/v1/tenant/api-keys";
    private string ApiKeyUrl(Guid id) => $"/v1/tenant/api-keys/{id}";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    [Fact]
    public async Task CreateApiKey_AsAdmin_Returns201WithRawKey()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = "My integration key",
            scopes = new[] { ApiKeyScope.BookingsRead }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.ReadFromApiJsonAsync<CreateApiKeyResult>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
        result.RawKey.Should().StartWith("cth_");
        result.Scopes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateApiKey_AsStaff_Returns403()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantStaff");

        var response = await client.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = "Staff key attempt",
            scopes = new[] { ApiKeyScope.BookingsRead }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListApiKeys_AsAdmin_ReturnsKeys()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create a key so there's at least one to list
        var createResp = await client.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"List test key {Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsRead, ApiKeyScope.StaffRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        // List and verify it appears
        var listResp = await client.GetAsync(ApiKeysUrl);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var keys = await listResp.ReadFromApiJsonAsync<List<ApiKeyDto>>();
        keys.Should().NotBeNull();
        keys!.Should().Contain(k => k.Id == created!.Id);
        var found = keys.First(k => k.Id == created!.Id);
        found.Scopes.Should().Contain(ApiKeyScope.BookingsRead);
    }

    [Fact]
    public async Task RevokeApiKey_AsAdmin_Returns204()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create a key to revoke
        var createResp = await client.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"Revoke test key {Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        // Revoke it
        var deleteResp = await client.DeleteAsync(ApiKeyUrl(created!.Id));
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AuthenticateWithApiKey_ValidKey_SucceedsOnProtectedEndpoint()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");

        // Create a key with tenant:read scope (required by ListApiKeysEndpoint)
        var createResp = await adminClient.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"Auth test key {Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TenantRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();
        var rawKey = created!.RawKey;

        // Use the raw key as X-Api-Key header (no Bearer token)
        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        var listResp = await apiKeyClient.GetAsync(ApiKeysUrl);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthenticateWithApiKey_RevokedKey_Returns401()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");

        // Create a key
        var createResp = await adminClient.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"Revoked auth test key {Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TenantRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();
        var rawKey = created!.RawKey;

        // Revoke the key
        var revokeResp = await adminClient.DeleteAsync(ApiKeyUrl(created.Id));
        revokeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Attempt to use the revoked key
        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        var listResp = await apiKeyClient.GetAsync(ApiKeysUrl);
        listResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeApiKey_NotFound_Returns404()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);

        var client = fixture.CreateClient("TenantAdmin");
        var nonExistentId = Guid.NewGuid();
        var response = await client.DeleteAsync($"/v1/tenant/api-keys/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateApiKey_WithUnknownScope_Returns400()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = "Bad scope key",
            scopes = new[] { "totally:invalid" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateApiKey_WithEmptyScopes_Returns400()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = "No scopes key",
            scopes = Array.Empty<string>()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ApiKeyWithoutRequiredScope_Returns403()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");

        // Create a key with bookings:read only (NOT tenant:read)
        var createResp = await adminClient.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"Narrow scope key {Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        // Try to list API keys — requires tenant:read scope
        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var listResp = await apiKeyClient.GetAsync(ApiKeysUrl);
        listResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ApiKeyWithMatchingScope_Returns200()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");

        // Create a key with tenant:read
        var createResp = await adminClient.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"Tenant read key {Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TenantRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        // List API keys — requires tenant:read scope → should succeed
        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var listResp = await apiKeyClient.GetAsync(ApiKeysUrl);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
