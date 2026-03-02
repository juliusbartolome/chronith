using Testcontainers.PostgreSql;

namespace Chronith.Tests.Integration.Fixtures;

public sealed class PostgresFixture : IAsyncLifetime
{
    private static readonly string? CiConnectionString =
        Environment.GetEnvironmentVariable("CI_INTEGRATION_CONNECTION_STRING");

    private readonly PostgreSqlContainer? _container = CiConnectionString is null
        ? new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("chronith_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build()
        : null;

    public string ConnectionString =>
        CiConnectionString ?? _container!.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (_container is not null)
            await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<PostgresFixture> { }
