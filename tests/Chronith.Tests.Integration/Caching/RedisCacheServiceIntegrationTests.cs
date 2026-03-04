using Chronith.Infrastructure.Caching;
using Chronith.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Integration.Caching;

[Collection("Redis")]
public sealed class RedisCacheServiceIntegrationTests(RedisFixture fixture)
{
    private RedisCacheService BuildService()
    {
        var services = new ServiceCollection();
        services.AddStackExchangeRedisCache(o =>
        {
            o.Configuration = fixture.ConnectionString;
        });
        var sp = services.BuildServiceProvider();
        var cache = sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        return new RedisCacheService(cache);
    }

    [Fact]
    public async Task GetOrSetAsync_CachesMissValue_AndReturnsOnHit()
    {
        var svc = BuildService();
        var key = $"test:{Guid.NewGuid()}";
        var callCount = 0;

        var first = await svc.GetOrSetAsync(
            key,
            () => { callCount++; return Task.FromResult("hello"); },
            TimeSpan.FromMinutes(1));

        var second = await svc.GetOrSetAsync(
            key,
            () => { callCount++; return Task.FromResult("should-not-be-called"); },
            TimeSpan.FromMinutes(1));

        first.Should().Be("hello");
        second.Should().Be("hello");
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task InvalidateAsync_RemovesCachedValue()
    {
        var svc = BuildService();
        var key = $"test:{Guid.NewGuid()}";

        await svc.GetOrSetAsync(key, () => Task.FromResult("value"), TimeSpan.FromMinutes(1));
        await svc.InvalidateAsync(key);

        var callCount = 0;
        var result = await svc.GetOrSetAsync(
            key,
            () => { callCount++; return Task.FromResult("refreshed"); },
            TimeSpan.FromMinutes(1));

        result.Should().Be("refreshed");
        callCount.Should().Be(1);
    }
}
