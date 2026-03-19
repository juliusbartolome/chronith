using Chronith.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Chronith.Tests.Functional.Fixtures;

public sealed class FunctionalTestFixture : IAsyncLifetime
{
    private static readonly string? CiConnectionString =
        Environment.GetEnvironmentVariable("CI_FUNCTIONAL_CONNECTION_STRING");

    private readonly PostgreSqlContainer? _postgres = CiConnectionString is null
        ? new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("chronith_functional")
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

        Factory = new WebApplicationFactory<Program>() // lgtm[cs/local-not-disposed] // codeql[cs/local-not-disposed]
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Database:Provider", "PostgreSQL");
                builder.UseSetting("Database:ConnectionString", connectionString);
                builder.UseSetting("Jwt:SigningKey", TestConstants.JwtSigningKey);
                builder.UseSetting("Security:EncryptionKeyVersion", "v1");
                builder.UseSetting("Security:KeyVersions:v1", TestConstants.EncryptionKey);
                builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
                // Raise rate-limit permits very high so functional tests never exhaust
                // the shared in-process IP bucket. Rate-limit shape tests use their own
                // isolated WebApplicationFactory instances with tight limits.
                builder.UseSetting("RateLimiting:Auth:PermitLimit", "10000");
                builder.UseSetting("RateLimiting:Auth:WindowSeconds", "300");
                builder.UseSetting("RateLimiting:Authenticated:PermitLimit", "10000");
                builder.UseSetting("RateLimiting:Authenticated:WindowSeconds", "300");
            });

        // Run migrations
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChronithDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    public HttpClient CreateClient(string role, string? userId = null, Guid? tenantId = null) =>
        CreateClientWithToken(TestJwtFactory.CreateToken(role, userId ?? RoleToUserId(role), tenantId));

    public HttpClient CreateAnonymousClient() =>
        Factory.CreateClient();

    public HttpClient CreateClientWithCustomerToken(string customerId, Guid? tenantId = null) =>
        CreateClientWithToken(TestJwtFactory.CreateCustomerToken(customerId, tenantId));

    private HttpClient CreateClientWithToken(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string RoleToUserId(string role) => role switch
    {
        "TenantAdmin"          => TestConstants.AdminUserId,
        "TenantStaff"          => TestConstants.StaffUserId,
        "Customer"             => TestConstants.CustomerUserId,
        "TenantPaymentService" => TestConstants.PaymentSvcUserId,
        _                      => "user-unknown"
    };
}

[CollectionDefinition("Functional")]
public class FunctionalCollection : ICollectionFixture<FunctionalTestFixture> { }
