using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Subscriptions;

[Collection("Functional")]
public sealed class SubscriptionAuthTests(FunctionalTestFixture fixture)
{
    private static readonly Guid SubTenantId = Guid.Parse("00000000-0000-0000-0000-000000000099");
    private static readonly Guid FreePlanId  = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private async Task EnsureTenantAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory, SubTenantId);
        await SeedData.SeedTenantAsync(db, SubTenantId, "sub-test-tenant");
    }

    // ── GET /v1/tenant/subscription — only TenantAdmin ──────────────────────

    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetSubscription_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureTenantAsync();
        var client = fixture.CreateClient(role, tenantId: SubTenantId);
        var response = await client.GetAsync("/v1/tenant/subscription");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetSubscription_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/v1/tenant/subscription");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /v1/tenant/subscribe — only TenantAdmin ─────────────────────

    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    public async Task Subscribe_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureTenantAsync();
        var client = fixture.CreateClient(role, tenantId: SubTenantId);
        string? paymentToken = null;
        var response = await client.PostAsJsonAsync("/v1/tenant/subscribe", new
        {
            planId = FreePlanId,
            paymentMethodToken = paymentToken
        });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Subscribe_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        string? paymentToken = null;
        var response = await client.PostAsJsonAsync("/v1/tenant/subscribe", new
        {
            planId = FreePlanId,
            paymentMethodToken = paymentToken
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT /v1/tenant/subscription/plan — only TenantAdmin ─────────────────

    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    public async Task ChangePlan_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureTenantAsync();
        var client = fixture.CreateClient(role, tenantId: SubTenantId);
        var response = await client.PutAsJsonAsync("/v1/tenant/subscription/plan", new
        {
            newPlanId = Guid.Parse("00000000-0000-0000-0000-000000000002")
        });
        response.StatusCode.Should().Be(expected);
    }

    // ── DELETE /v1/tenant/subscription — only TenantAdmin ───────────────────

    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    public async Task CancelSubscription_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureTenantAsync();
        var client = fixture.CreateClient(role, tenantId: SubTenantId);
        string? reason = null;
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/v1/tenant/subscription")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { reason })
        };
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(expected);
    }

    // ── GET /v1/tenant/usage — TenantAdmin + TenantStaff ────────────────────

    [Fact]
    public async Task GetUsage_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/v1/tenant/usage");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetUsage_NonTenantRole_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureTenantAsync();
        var client = fixture.CreateClient(role, tenantId: SubTenantId);
        var response = await client.GetAsync("/v1/tenant/usage");
        response.StatusCode.Should().Be(expected);
    }

    // ── API Key scope tests ──────────────────────────────────────────────────

    [Fact]
    public async Task GetSubscription_WithApiKey_WithTenantReadScope_Returns200()
    {
        await EnsureTenantAsync();
        var adminClient = fixture.CreateClient("TenantAdmin", tenantId: SubTenantId);
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TenantRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.GetAsync("/v1/tenant/subscription");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Subscribe_WithApiKey_WithTenantWriteScope_Returns201()
    {
        // Use a dedicated tenant ID to avoid collision with SubscriptionEndpointsTests
        var apiKeySubTenantId = Guid.Parse("00000000-0000-0000-0000-0000000000A2");
        await using var db = SeedData.CreateDbContext(fixture.Factory, apiKeySubTenantId);
        await SeedData.SeedTenantAsync(db, apiKeySubTenantId, "apikey-sub-test-tenant");

        var adminClient = fixture.CreateClient("TenantAdmin", tenantId: apiKeySubTenantId);
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.TenantWrite }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        string? paymentToken = null;
        var response = await apiKeyClient.PostAsJsonAsync("/v1/tenant/subscribe", new
        {
            planId = FreePlanId,
            paymentMethodToken = paymentToken
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
