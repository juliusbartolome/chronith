namespace Chronith.Infrastructure.Services;

public interface IBackgroundServiceHealthTracker
{
    void RecordSuccess(string serviceName);
    DateTimeOffset? GetLastSuccess(string serviceName);
}
