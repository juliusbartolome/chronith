using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Functional.Subscriptions;

[Collection("Functional")]
public sealed class SubscriptionEndpointsTests(FunctionalTestFixture fixture)
{
    // Use a different tenant to avoid collision with FreePlanId (00000000-0000-0000-0000-000000000001)
    private static readonly Guid SubTenantId = Guid.Parse("00000000-0000-0000-0000-000000000099");
    private static readonly Guid FreePlanId  = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid StarterPlanId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private async Task EnsureTenantAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory, SubTenantId);
        await SeedData.SeedTenantAsync(db, SubTenantId, "sub-test-tenant");
    }

    private async Task SeedSubscriptionAsync(string status = "Trialing")
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory, SubTenantId);

        // Remove existing active subscriptions for this tenant to keep tests idempotent
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM chronith.tenant_subscriptions
            WHERE "TenantId" = {0}
            """, SubTenantId);

        db.TenantSubscriptions.Add(new TenantSubscriptionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = SubTenantId,
            PlanId = FreePlanId,
            Status = status,
            TrialEndsAt = DateTimeOffset.UtcNow.AddDays(14),
            CurrentPeriodStart = DateTimeOffset.UtcNow,
            CurrentPeriodEnd = DateTimeOffset.UtcNow.AddDays(30),
            PaymentProviderSubscriptionId = null,
            CreatedAt = DateTimeOffset.UtcNow,
            CancelledAt = null,
            CancelReason = null,
            IsDeleted = false,
        });
        await db.SaveChangesAsync();
    }

    private HttpClient AdminClient() =>
        fixture.CreateClient("TenantAdmin", tenantId: SubTenantId);

    // ── GET /v1/tenant/subscription ─────────────────────────────────────────

    [Fact]
    public async Task GetSubscription_WhenActive_Returns200WithDto()
    {
        await EnsureTenantAsync();
        await SeedSubscriptionAsync();
        var client = AdminClient();

        var response = await client.GetAsync("/v1/tenant/subscription");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadFromApiJsonAsync<TenantSubscriptionDto>();
        body.Should().NotBeNull();
        body!.TenantId.Should().Be(SubTenantId);
        body.PlanName.Should().Be("Free");
        body.Status.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSubscription_WhenNoSubscription_Returns404()
    {
        await EnsureTenantAsync();
        // Ensure no active subscription
        await using var db = SeedData.CreateDbContext(fixture.Factory, SubTenantId);
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM chronith.tenant_subscriptions WHERE "TenantId" = {0}
            """, SubTenantId);

        var client = AdminClient();
        var response = await client.GetAsync("/v1/tenant/subscription");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /v1/tenant/subscription ────────────────────────────────────────

    [Fact]
    public async Task Subscribe_ToFreePlan_Returns201WithTrialingStatus()
    {
        await EnsureTenantAsync();
        // Ensure no active subscription
        await using var db = SeedData.CreateDbContext(fixture.Factory, SubTenantId);
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM chronith.tenant_subscriptions WHERE "TenantId" = {0}
            """, SubTenantId);

        var client = AdminClient();
        var response = await client.PostAsJsonAsync("/v1/tenant/subscription", new
        {
            planId = FreePlanId,
            paymentMethodToken = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadFromApiJsonAsync<TenantSubscriptionDto>();
        body.Should().NotBeNull();
        body!.PlanId.Should().Be(FreePlanId);
        body.TenantId.Should().Be(SubTenantId);
    }

    [Fact]
    public async Task Subscribe_WhenAlreadySubscribed_Returns409()
    {
        await EnsureTenantAsync();
        await SeedSubscriptionAsync();

        var client = AdminClient();
        var response = await client.PostAsJsonAsync("/v1/tenant/subscription", new
        {
            planId = FreePlanId,
            paymentMethodToken = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── PUT /v1/tenant/subscription/plan ────────────────────────────────────

    [Fact]
    public async Task ChangePlan_ToStarter_Returns200WithNewPlan()
    {
        await EnsureTenantAsync();
        await SeedSubscriptionAsync();

        var client = AdminClient();
        var response = await client.PutAsJsonAsync("/v1/tenant/subscription/plan", new
        {
            newPlanId = StarterPlanId
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadFromApiJsonAsync<TenantSubscriptionDto>();
        body.Should().NotBeNull();
        body!.PlanId.Should().Be(StarterPlanId);
        body.PlanName.Should().Be("Starter");
    }

    [Fact]
    public async Task ChangePlan_WhenNoSubscription_Returns404()
    {
        await EnsureTenantAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory, SubTenantId);
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM chronith.tenant_subscriptions WHERE "TenantId" = {0}
            """, SubTenantId);

        var client = AdminClient();
        var response = await client.PutAsJsonAsync("/v1/tenant/subscription/plan", new
        {
            newPlanId = StarterPlanId
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /v1/tenant/subscription ──────────────────────────────────────

    [Fact]
    public async Task CancelSubscription_WhenActive_Returns204()
    {
        await EnsureTenantAsync();
        await SeedSubscriptionAsync();

        var client = AdminClient();
        var request = new HttpRequestMessage(HttpMethod.Delete, "/v1/tenant/subscription")
        {
            Content = JsonContent.Create(new { reason = "Testing cancellation" })
        };
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── GET /v1/tenant/usage ─────────────────────────────────────────────────

    [Fact]
    public async Task GetUsage_WhenSubscribed_Returns200WithDto()
    {
        await EnsureTenantAsync();
        await SeedSubscriptionAsync();

        var client = AdminClient();
        var response = await client.GetAsync("/v1/tenant/usage");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadFromApiJsonAsync<TenantUsageDto>();
        body.Should().NotBeNull();
        body!.PlanName.Should().Be("Free");
        body.BookingTypesUsed.Should().BeGreaterThanOrEqualTo(0);
        body.BookingsThisMonth.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetUsage_AsStaff_Returns200()
    {
        await EnsureTenantAsync();
        // Seed a fresh subscription to ensure one exists regardless of test ordering
        await using var db = SeedData.CreateDbContext(fixture.Factory, SubTenantId);
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM chronith.tenant_subscriptions WHERE "TenantId" = {0}
            """, SubTenantId);
        db.TenantSubscriptions.Add(new TenantSubscriptionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = SubTenantId,
            PlanId = FreePlanId,
            Status = "Trialing",
            TrialEndsAt = DateTimeOffset.UtcNow.AddDays(14),
            CurrentPeriodStart = DateTimeOffset.UtcNow,
            CurrentPeriodEnd = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false,
        });
        await db.SaveChangesAsync();

        var client = fixture.CreateClient("TenantStaff", tenantId: SubTenantId);
        var response = await client.GetAsync("/v1/tenant/usage");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
