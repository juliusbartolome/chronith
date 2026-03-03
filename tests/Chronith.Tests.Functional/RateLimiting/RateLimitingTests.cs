using System.Net;
using Chronith.Infrastructure.Persistence;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Chronith.Tests.Functional.RateLimiting;

/// <summary>
/// Rate limiting tests use self-contained WebApplicationFactory instances with
/// UseSetting overrides so each test can configure per-tenant permit limits
/// without polluting the shared FunctionalTestFixture.
/// </summary>
public class RateLimitingTests : IAsyncLifetime
{
    // Standard factory — default 300 req/min limit
    private WebApplicationFactory<Program> _defaultFactory = null!;
    // Low-limit factory — 1 req/min for TestConstants.TenantId
    private WebApplicationFactory<Program> _lowLimitFactory = null!;

    private static readonly string? CiConnectionString =
        Environment.GetEnvironmentVariable("CI_FUNCTIONAL_CONNECTION_STRING");

    private readonly Testcontainers.PostgreSql.PostgreSqlContainer? _postgres =
        CiConnectionString is null
            ? new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("chronith_ratelimit")
                .WithUsername("test")
                .WithPassword("test")
                .Build()
            : null;

    public async Task InitializeAsync()
    {
        if (_postgres is not null)
            await _postgres.StartAsync();

        var connStr = CiConnectionString ?? _postgres!.GetConnectionString();
        var tenantOverrideKey =
            $"RateLimiting:TenantOverrides:{TestConstants.TenantId}:PermitLimit";

        _defaultFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("Database:Provider", "PostgreSQL");
                b.UseSetting("Database:ConnectionString", connStr);
                b.UseSetting("Jwt:SigningKey", TestConstants.JwtSigningKey);
            });

        _lowLimitFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("Database:Provider", "PostgreSQL");
                b.UseSetting("Database:ConnectionString", connStr);
                b.UseSetting("Jwt:SigningKey", TestConstants.JwtSigningKey);
                b.UseSetting(tenantOverrideKey, "1");
            });

        // Run migrations once (both factories share the same DB)
        using var scope = _defaultFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChronithDbContext>();
        await db.Database.MigrateAsync();

        // Seed the shared tenant
        using var db2 = SeedData.CreateDbContext(_defaultFactory);
        await SeedData.SeedTenantAsync(db2);
    }

    public async Task DisposeAsync()
    {
        await _defaultFactory.DisposeAsync();
        await _lowLimitFactory.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    private HttpClient DefaultClient()
    {
        var client = _defaultFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", TestJwtFactory.CreateToken("TenantAdmin", TestConstants.AdminUserId));
        return client;
    }

    private HttpClient LowLimitClient()
    {
        var client = _lowLimitFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", TestJwtFactory.CreateToken("TenantAdmin", TestConstants.AdminUserId));
        return client;
    }

    [Fact]
    public async Task Request_UnderLimit_IncludesRateLimitHeaders()
    {
        var client = DefaultClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("X-RateLimit-Limit");
        response.Headers.Should().ContainKey("X-RateLimit-Reset");
    }

    [Fact]
    public async Task Request_OverLimit_Returns429WithRetryAfterHeader()
    {
        var client = LowLimitClient();

        // First request consumes the 1 available permit
        var first = await client.GetAsync("/health/live");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second request exceeds the 1 req/window limit
        var second = await client.GetAsync("/health/live");
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        second.Headers.Should().ContainKey("Retry-After");
    }

    [Fact]
    public async Task TwoTenants_HaveIsolatedRateLimitCounters()
    {
        // Tenant A (default TenantId) has permit limit of 1 in the low-limit factory
        var clientA = LowLimitClient();

        // Tenant B uses a different tenant ID — not overridden, so gets DefaultPermitLimit (300)
        var tenantBId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var clientB = _lowLimitFactory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", TestJwtFactory.CreateToken("TenantAdmin", "user-admin-2", tenantBId));

        // Exhaust tenant A's limit
        await clientA.GetAsync("/health/live");
        var secondA = await clientA.GetAsync("/health/live");
        secondA.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Tenant B should be unaffected
        var responseB = await clientB.GetAsync("/health/live");
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
