using Chronith.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Chronith.Tests.Functional.Fixtures;

public sealed class FunctionalTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("chronith_functional")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Database:Provider", "PostgreSQL");
                builder.UseSetting("Database:ConnectionString", _postgres.GetConnectionString());
                builder.UseSetting("Jwt:SigningKey", TestConstants.JwtSigningKey);
            });

        // Run migrations
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChronithDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient CreateClient(string role, string? userId = null) =>
        CreateClientWithToken(TestJwtFactory.CreateToken(role, userId ?? RoleToUserId(role)));

    public HttpClient CreateAnonymousClient() =>
        Factory.CreateClient();

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
