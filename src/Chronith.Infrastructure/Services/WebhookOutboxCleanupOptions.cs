namespace Chronith.Infrastructure.Services;

public sealed class WebhookOutboxCleanupOptions
{
    public const string SectionName = "WebhookOutboxCleanup";

    /// <summary>Retain rows for this many days after creation. Default: 30.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>How often to run the cleanup. Default: every 6 hours.</summary>
    public int IntervalHours { get; set; } = 6;
}
