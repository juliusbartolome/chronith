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
/// UseSetting overrides so each test can configure per-policy permit limits
/// without polluting the shared FunctionalTestFixture.
/// </summary>
public class RateLimitingTests : IAsyncLifetime
{
    // Standard factory — default 300 req/min authenticated limit
    private WebApplicationFactory<Program> _defaultFactory = null!;
    // Tight-limit factory — Authenticated policy capped at 1 req/window
    private WebApplicationFactory<Program> _tightLimitFactory = null!;
    // Tight auth-policy factory — Auth policy (login/register) capped at 1 req/window
    private WebApplicationFactory<Program> _tightAuthPolicyFactory = null!;

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

        _defaultFactory = new WebApplicationFactory<Program>() // lgtm[cs/local-not-disposed] // codeql[cs/local-not-disposed]
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("Database:Provider", "PostgreSQL");
                b.UseSetting("Database:ConnectionString", connStr);
                b.UseSetting("Jwt:SigningKey", TestConstants.JwtSigningKey);
                b.UseSetting("Security:EncryptionKeyVersion", "v1");
                b.UseSetting("Security:KeyVersions:v1", TestConstants.EncryptionKey);
            });

        _tightLimitFactory = new TightLimitWebApplicationFactory(connStr); // codeql[cs/local-not-disposed]

        _tightAuthPolicyFactory = new TightAuthPolicyWebApplicationFactory(connStr); // codeql[cs/local-not-disposed]

        // Run migrations once (all factories share the same DB)
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
        await _tightLimitFactory.DisposeAsync();
        await _tightAuthPolicyFactory.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    private HttpClient AuthenticatedClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", TestJwtFactory.CreateToken("TenantAdmin", TestConstants.AdminUserId));
        return client;
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Authenticated requests within the permit limit should return 200.
    /// </summary>
    [Fact]
    public async Task AuthenticatedRequest_UnderLimit_Returns200()
    {
        var client = AuthenticatedClient(_defaultFactory);

        // GET /v1/booking-types is an Authenticated-policy endpoint that returns
        // an empty list when no booking types exist — it never needs seeded data.
        var response = await client.GetAsync("/v1/booking-types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// When the Authenticated policy permit limit is set to 1, the second
    /// request from the same tenant partition should receive HTTP 429.
    /// </summary>
    [Fact]
    public async Task AuthenticatedRequest_OverLimit_Returns429()
    {
        var client = AuthenticatedClient(_tightLimitFactory);

        // First request — within limit
        var first = await client.GetAsync("/v1/booking-types");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second request — exceeds the 1-request window
        var second = await client.GetAsync("/v1/booking-types");
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// When rate limited, the response body should be a ProblemDetails JSON
    /// document and the Retry-After header should be present.
    /// </summary>
    [Fact]
    public async Task RateLimited_Response_HasRetryAfterHeaderAndProblemDetails()
    {
        var client = AuthenticatedClient(_tightLimitFactory);

        // Exhaust the 1-request limit
        await client.GetAsync("/v1/booking-types");

        var response = await client.GetAsync("/v1/booking-types");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.Should().ContainKey("Retry-After");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    /// <summary>
    /// Two tenants' Authenticated-policy partitions are isolated: exhausting
    /// tenant A's limit must not affect tenant B.
    /// </summary>
    [Fact]
    public async Task TwoTenants_HaveIsolatedRateLimitCounters()
    {
        // Tenant A — the seeded TenantId; limit is 1
        var clientA = AuthenticatedClient(_tightLimitFactory);

        // Tenant B — different tenant_id claim; not overridden, still gets PermitLimit=1
        // (since the tight factory sets the global Authenticated limit to 1)
        // So instead we compare partition keys: Tenant A's second request vs
        // a *new* client that looks like a different tenant via IP.
        // The Authenticated policy partitions by tenant_id JWT claim.
        // Use a fresh tenantId to ensure a separate bucket.
        var tenantBId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var clientB = _tightLimitFactory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", TestJwtFactory.CreateToken("TenantAdmin", "user-admin-2", tenantBId));

        // Exhaust tenant A's limit
        await clientA.GetAsync("/v1/booking-types");
        var secondA = await clientA.GetAsync("/v1/booking-types");
        secondA.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Tenant B has its own counter — should still succeed on first request
        var responseB = await clientB.GetAsync("/v1/booking-types");
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The Auth policy (login / register endpoints) should enforce its own
    /// permit limit independently of the Authenticated policy.
    /// </summary>
    [Fact]
    public async Task AuthPolicy_OverLimit_Returns429()
    {
        var client = _tightAuthPolicyFactory.CreateClient();

        // POST to /v1/auth/login with an invalid body just to exercise the rate limiter —
        // we expect either 400 (validation) or 401 (bad credentials) for valid requests,
        // but the key assertion is that after the first request the second returns 429.
        using var payload = new StringContent(
            """{"tenantSlug":"x","email":"x@x.com","password":"wrong"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        // First request — within Auth limit
        var first = await client.PostAsync("/v1/auth/login", payload);
        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

        // Second request — exceeds 1-request Auth window
        using var secondPayload = new StringContent(
            """{"tenantSlug":"x","email":"x@x.com","password":"wrong"}""",
            System.Text.Encoding.UTF8,
            "application/json");
        var second = await client.PostAsync("/v1/auth/login", secondPayload);
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    // ── Nested factory subclasses — avoid intermediate disposable objects ─────

    private sealed class TightLimitWebApplicationFactory(string connStr) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Database:Provider", "PostgreSQL");
            builder.UseSetting("Database:ConnectionString", connStr);
            builder.UseSetting("Jwt:SigningKey", TestConstants.JwtSigningKey);
            builder.UseSetting("Security:EncryptionKeyVersion", "v1");
            builder.UseSetting("Security:KeyVersions:v1", TestConstants.EncryptionKey);
            // Authenticated policy capped at 1 req/window
            builder.UseSetting("RateLimiting:Authenticated:PermitLimit", "1");
            builder.UseSetting("RateLimiting:Authenticated:WindowSeconds", "60");
        }
    }

    private sealed class TightAuthPolicyWebApplicationFactory(string connStr) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Database:Provider", "PostgreSQL");
            builder.UseSetting("Database:ConnectionString", connStr);
            builder.UseSetting("Jwt:SigningKey", TestConstants.JwtSigningKey);
            builder.UseSetting("Security:EncryptionKeyVersion", "v1");
            builder.UseSetting("Security:KeyVersions:v1", TestConstants.EncryptionKey);
            // Auth policy (login/register) capped at 1 req/window
            builder.UseSetting("RateLimiting:Auth:PermitLimit", "1");
            builder.UseSetting("RateLimiting:Auth:WindowSeconds", "60");
        }
    }
}
