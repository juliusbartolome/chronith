namespace Chronith.Application.Interfaces;

public interface IRedisCacheService
{
    /// <summary>
    /// Returns cached value for <paramref name="key"/>, or calls <paramref name="factory"/>,
    /// caches the result for <paramref name="ttl"/>, then returns it.
    /// </summary>
    Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan ttl,
        CancellationToken ct = default);

    /// <summary>Removes the cached entry for <paramref name="key"/>.</summary>
    Task InvalidateAsync(string key, CancellationToken ct = default);
}
