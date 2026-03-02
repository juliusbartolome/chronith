namespace Chronith.Infrastructure.Services;

public sealed class WebhookDispatcherOptions
{
    public int DispatchIntervalSeconds { get; set; } = 10;
    public int HttpTimeoutSeconds { get; set; } = 10;
}
