using Testcontainers.PostgreSql;

namespace Chronith.Tests.Integration.Fixtures;

public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("chronith_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public Task InitializeAsync() => Container.StartAsync();
    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<PostgresFixture> { }
