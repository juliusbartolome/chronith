using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.ApiKeys;

[Collection("Functional")]
public sealed class ApiKeyEndpointsTests(FunctionalTestFixture fixture)
{
    private const string ApiKeysUrl = "/tenant/api-keys";
    private string ApiKeyUrl(Guid id) => $"/tenant/api-keys/{id}";

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
            role = "TenantAdmin"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateApiKeyResult>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
        result.RawKey.Should().StartWith("cth_");
    }

    [Fact]
    public async Task CreateApiKey_AsStaff_Returns403()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantStaff");

        var response = await client.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = "Staff key attempt",
            role = "TenantStaff"
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
            role = "TenantAdmin"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<CreateApiKeyResult>();

        // List and verify it appears
        var listResp = await client.GetAsync(ApiKeysUrl);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var keys = await listResp.Content.ReadFromJsonAsync<List<ApiKeyDto>>();
        keys.Should().NotBeNull();
        keys!.Should().Contain(k => k.Id == created!.Id);
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
            role = "TenantAdmin"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<CreateApiKeyResult>();

        // Revoke it
        var deleteResp = await client.DeleteAsync(ApiKeyUrl(created!.Id));
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AuthenticateWithApiKey_ValidKey_SucceedsOnProtectedEndpoint()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");

        // Create a key
        var createResp = await adminClient.PostAsJsonAsync(ApiKeysUrl, new
        {
            description = $"Auth test key {Guid.NewGuid():N}",
            role = "TenantAdmin"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<CreateApiKeyResult>();
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
            role = "TenantAdmin"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<CreateApiKeyResult>();
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
}
