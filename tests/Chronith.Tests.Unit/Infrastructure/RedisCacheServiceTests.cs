using Chronith.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using System.Text.Json;

namespace Chronith.Tests.Unit.Infrastructure;

public class RedisCacheServiceTests
{
    private readonly IDistributedCache _cache = Substitute.For<IDistributedCache>();
    private readonly RedisCacheService _sut;

    public RedisCacheServiceTests() => _sut = new RedisCacheService(_cache);

    [Fact]
    public async Task GetOrSetAsync_CacheHit_ReturnsCachedValue_WithoutCallingFactory()
    {
        const string key = "test-key";
        var expected = new TestPayload("hello");
        var json = JsonSerializer.SerializeToUtf8Bytes(expected);

        _cache.GetAsync(key, Arg.Any<CancellationToken>())
              .Returns(json);

        var callCount = 0;
        var result = await _sut.GetOrSetAsync<TestPayload>(
            key,
            () => { callCount++; return Task.FromResult(new TestPayload("miss")); },
            TimeSpan.FromSeconds(30));

        result.Should().BeEquivalentTo(expected);
        callCount.Should().Be(0);
    }

    [Fact]
    public async Task GetOrSetAsync_CacheMiss_CallsFactory_AndSetsCache()
    {
        const string key = "miss-key";
        var value = new TestPayload("world");

        _cache.GetAsync(key, Arg.Any<CancellationToken>())
              .Returns((byte[]?)null);

        var result = await _sut.GetOrSetAsync<TestPayload>(
            key,
            () => Task.FromResult(value),
            TimeSpan.FromSeconds(60));

        result.Should().BeEquivalentTo(value);
        await _cache.Received(1).SetAsync(
            key,
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromSeconds(60)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_CallsRemoveOnCache()
    {
        const string key = "remove-key";

        await _sut.InvalidateAsync(key);

        await _cache.Received(1).RemoveAsync(key, Arg.Any<CancellationToken>());
    }

    private sealed record TestPayload(string Value);
}
