using System.Collections.Concurrent;

namespace Chronith.Infrastructure.Services;

public sealed class BackgroundServiceHealthTracker : IBackgroundServiceHealthTracker
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSuccess = new();

    public void RecordSuccess(string serviceName) =>
        _lastSuccess[serviceName] = DateTimeOffset.UtcNow;

    public DateTimeOffset? GetLastSuccess(string serviceName) =>
        _lastSuccess.TryGetValue(serviceName, out var ts) ? ts : null;
}
