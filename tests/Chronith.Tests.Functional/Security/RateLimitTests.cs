using System.Net;
using System.Net.Http.Json;
using Chronith.Infrastructure.Persistence;
using Chronith.Tests.Functional.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chronith.Tests.Functional.Security;

/// <summary>
/// Rate-limit response shape tests.  Uses a self-contained
/// WebApplicationFactory with a tight Auth policy (1 req/window)
/// so it never pollutes the shared FunctionalTestFixture IP bucket.
/// </summary>
public sealed class RateLimitTests : IAsyncLifetime
{
    private static readonly string? CiConnectionString =
        Environment.GetEnvironmentVariable("CI_FUNCTIONAL_CONNECTION_STRING");

    private readonly Testcontainers.PostgreSql.PostgreSqlContainer? _postgres =
        CiConnectionString is null
            ? new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("chronith_ratelimitshape")
                .WithUsername("test")
                .WithPassword("test")
                .Build()
            : null;

    private WebApplicationFactory<Program> _tightAuthFactory = null!;

    public async Task InitializeAsync()
    {
        if (_postgres is not null)
            await _postgres.StartAsync();

        var connStr = CiConnectionString ?? _postgres!.GetConnectionString();

        _tightAuthFactory = new WebApplicationFactory<Program>() // lgtm[cs/local-not-disposed] // codeql[cs/local-not-disposed]
            .WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Development");
                b.UseSetting("Database:Provider", "PostgreSQL");
                b.UseSetting("Database:ConnectionString", connStr);
                b.UseSetting("Jwt:SigningKey", TestConstants.JwtSigningKey);
                b.UseSetting("Security:EncryptionKey", TestConstants.EncryptionKey);
                // Auth policy: 1 request per 300-second window
                b.UseSetting("RateLimiting:Auth:PermitLimit", "1");
                b.UseSetting("RateLimiting:Auth:WindowSeconds", "300");
            });

        using var scope = _tightAuthFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChronithDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _tightAuthFactory.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    private const string AuthLoginPath = "/v1/auth/login";

    private static readonly object InvalidLoginPayload = new
    {
        tenantSlug = "does-not-exist",
        email = "nobody@nowhere.test",
        password = "wrong-password"
    };

    [Fact]
    public async Task AuthEndpoint_WhenRateLimitExceeded_Returns429()
    {
        var client = _tightAuthFactory.CreateClient();

        // First request — within limit (returns 400/404)
        await client.PostAsJsonAsync(AuthLoginPath, InvalidLoginPayload);

        // Second request — exceeds 1-request Auth window
        var limited = await client.PostAsJsonAsync(AuthLoginPath, InvalidLoginPayload);

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task AuthEndpoint_WhenRateLimitExceeded_ResponseHasRetryAfterHeader()
    {
        var client = _tightAuthFactory.CreateClient();

        await client.PostAsJsonAsync(AuthLoginPath, InvalidLoginPayload);
        var limitedResponse = await client.PostAsJsonAsync(AuthLoginPath, InvalidLoginPayload);

        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limitedResponse.Headers.Should().ContainKey("Retry-After");
    }

    [Fact]
    public async Task AuthEndpoint_WhenRateLimitExceeded_ResponseBodyIsProblemDetails()
    {
        var client = _tightAuthFactory.CreateClient();

        await client.PostAsJsonAsync(AuthLoginPath, InvalidLoginPayload);
        var limitedResponse = await client.PostAsJsonAsync(AuthLoginPath, InvalidLoginPayload);

        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limitedResponse.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json");
    }
}
