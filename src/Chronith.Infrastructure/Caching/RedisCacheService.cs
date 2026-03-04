using System.Text.Json;
using Chronith.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace Chronith.Infrastructure.Caching;

public sealed class RedisCacheService(IDistributedCache cache) : IRedisCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(key, ct);
        if (bytes is not null)
            return JsonSerializer.Deserialize<T>(bytes, JsonOptions);

        var value = await factory();
        if (value is not null)
        {
            var serialized = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
            await cache.SetAsync(key, serialized,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                }, ct);
        }

        return value;
    }

    public Task InvalidateAsync(string key, CancellationToken ct = default)
        => cache.RemoveAsync(key, ct);
}
