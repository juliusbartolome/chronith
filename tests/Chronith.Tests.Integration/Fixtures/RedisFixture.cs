using Testcontainers.Redis;

namespace Chronith.Tests.Integration.Fixtures;

public sealed class RedisFixture : IAsyncLifetime
{
    private static readonly string? CiConnectionString =
        Environment.GetEnvironmentVariable("CI_REDIS_CONNECTION_STRING");

    private readonly RedisContainer? _container = CiConnectionString is null
        ? new RedisBuilder("redis:8-alpine").Build()
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

[CollectionDefinition("Redis")]
public class RedisCollection : ICollectionFixture<RedisFixture> { }
