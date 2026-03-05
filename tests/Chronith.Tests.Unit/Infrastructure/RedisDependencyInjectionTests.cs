using Chronith.Application.Interfaces;
using Chronith.Infrastructure;
using Chronith.Infrastructure.Caching;
using Chronith.Infrastructure.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chronith.Tests.Unit.Infrastructure;

public class RedisDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_WithRedisDisabled_RegistersInMemoryRateLimitStore()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "PostgreSQL",
                ["Database:ConnectionString"] = "Host=localhost;Database=test;Username=u;Password=p",
                ["Redis:Enabled"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(config);

        using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IRateLimitStore>();
        store.Should().BeOfType<InMemoryRateLimitStore>();
    }

    [Fact]
    public void AddInfrastructure_WithRedisEnabled_RegistersRedisRateLimitStore()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "PostgreSQL",
                ["Database:ConnectionString"] = "Host=localhost;Database=test;Username=u;Password=p",
                ["Redis:Enabled"] = "true",
                ["Redis:ConnectionString"] = "localhost:6379"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(config);

        using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<IRateLimitStore>();
        store.Should().BeOfType<RedisRateLimitStore>();
    }

    [Fact]
    public void AddInfrastructure_WithRedisEnabled_RegistersIRedisCacheService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "PostgreSQL",
                ["Database:ConnectionString"] = "Host=localhost;Database=test;Username=u;Password=p",
                ["Redis:Enabled"] = "true",
                ["Redis:ConnectionString"] = "localhost:6379"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(config);

        using var sp = services.BuildServiceProvider();
        var cacheService = sp.GetRequiredService<IRedisCacheService>();
        cacheService.Should().BeOfType<RedisCacheService>();
    }
}
