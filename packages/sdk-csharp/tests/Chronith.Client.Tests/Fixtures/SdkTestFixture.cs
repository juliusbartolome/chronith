using System.Net.Http.Headers;
using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Chronith.Client.Tests.Fixtures;

/// <summary>
/// Shared WebApplicationFactory fixture for SDK integration tests.
/// Mirrors the pattern in tests/Chronith.Tests.Functional/Fixtures/FunctionalTestFixture.cs.
/// </summary>
public sealed class SdkTestFixture : IAsyncLifetime
{
    private static readonly string? CiConnectionString =
        Environment.GetEnvironmentVariable("CI_SDK_CONNECTION_STRING");

    private readonly PostgreSqlContainer? _postgres = CiConnectionString is null
        ? new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("chronith_sdk_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build()
        : null;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (_postgres is not null)
            await _postgres.StartAsync();

        var connectionString = CiConnectionString ?? _postgres!.GetConnectionString();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Database:Provider", "PostgreSQL");
                builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
                builder.UseSetting("Jwt:SigningKey", TestConstants.JwtSigningKey);
                builder.UseSetting("Security:EncryptionKey", TestConstants.EncryptionKey);
                builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
                // Raise rate-limit permits very high so SDK tests never exhaust
                // the shared in-process IP bucket.
                builder.UseSetting("RateLimiting:Auth:PermitLimit", "10000");
                builder.UseSetting("RateLimiting:Auth:WindowSeconds", "300");
                builder.UseSetting("RateLimiting:Authenticated:PermitLimit", "10000");
                builder.UseSetting("RateLimiting:Authenticated:WindowSeconds", "300");
            });

        // Run migrations
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChronithDbContext>();
        await db.Database.MigrateAsync();

        // Seed test tenant (idempotent) so all tenant-scoped endpoints have a valid tenant row.
        await EnsureTenantSeededAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Seeds the test tenant row (idempotent via ON CONFLICT DO NOTHING).
    /// Called automatically from <see cref="InitializeAsync"/> before any test executes.
    /// </summary>
    private async Task EnsureTenantSeededAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<DbContextOptions<ChronithDbContext>>();
        await using var db = new ChronithDbContext(options, new StubTenantContext(TestConstants.TenantId));
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO chronith.tenants ("Id","Slug","Name","TimeZoneId","IsDeleted","CreatedAt")
            VALUES ({0},{1},{2},{3},{4},{5})
            ON CONFLICT DO NOTHING
            """,
            TestConstants.TenantId, "test-tenant", "Test Tenant", "UTC", false, DateTimeOffset.UtcNow);
    }

    public ChronithClient CreateChronithClient(string role)
    {
        var httpClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var token = TestJwtFactory.CreateToken(role);
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return new ChronithClient(httpClient);
    }

    public ChronithClient CreateAnonymousChronithClient()
    {
        var httpClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new ChronithClient(httpClient);
    }

    private sealed class StubTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public string UserId => TestConstants.AdminUserId;
        public string Role => "TenantAdmin";
    }
}
