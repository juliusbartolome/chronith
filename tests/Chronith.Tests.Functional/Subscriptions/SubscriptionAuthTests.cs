using System.Net;
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
        var response = await client.PostAsJsonAsync("/v1/tenant/subscribe", new
        {
            planId = FreePlanId,
            paymentMethodToken = (string?)null
        });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Subscribe_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/v1/tenant/subscribe", new
        {
            planId = FreePlanId,
            paymentMethodToken = (string?)null
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
        var request = new HttpRequestMessage(HttpMethod.Delete, "/v1/tenant/subscription")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { reason = (string?)null })
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
}
