using System.Collections.Concurrent;
using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
                builder.UseSetting("RateLimiting:Export:PermitLimit", "10000");
                builder.UseSetting("RateLimiting:Export:WindowSeconds", "300");
                // HMAC payment page settings for signed URL generation
                builder.UseSetting("PaymentPage:BaseUrl", "https://test.example.com/pay");
                builder.UseSetting("PaymentPage:TokenLifetimeSeconds", "3600");

                // Replace Azure Blob Storage with in-memory stub so functional tests
                // that upload proof-of-payment files work without Azurite.
                builder.ConfigureTestServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IFileStorageService));
                    if (descriptor is not null)
                        services.Remove(descriptor);
                    services.AddSingleton<IFileStorageService, InMemoryFileStorageService>();
                });
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

/// <summary>
/// In-memory stub for <see cref="IFileStorageService"/> used by functional tests.
/// Stores blobs in a <see cref="ConcurrentDictionary{TKey,TValue}"/> and returns
/// deterministic URLs so assertions can verify proof-of-payment upload behaviour
/// without requiring Azurite or Azure Blob Storage.
/// </summary>
internal sealed class InMemoryFileStorageService : IFileStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new();

    public async Task<FileUploadResult> UploadAsync(
        string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var key = $"{containerName}/{fileName}";
        _blobs[key] = ms.ToArray();
        var url = $"https://memory.blob.local/{key}";
        return new FileUploadResult(url, fileName);
    }

    public Task<Stream?> DownloadAsync(string containerName, string fileName, CancellationToken ct = default)
    {
        var key = $"{containerName}/{fileName}";
        if (_blobs.TryGetValue(key, out var data))
        {
            // Caller owns the returned stream and is responsible for disposal.
            Stream result = new MemoryStream(data); // lgtm [cs/disposable-not-disposed]
            return Task.FromResult<Stream?>(result);
        }
        return Task.FromResult<Stream?>(null);
    }

    public Task DeleteAsync(string containerName, string fileName, CancellationToken ct = default)
    {
        _blobs.TryRemove($"{containerName}/{fileName}", out _);
        return Task.CompletedTask;
    }
}
