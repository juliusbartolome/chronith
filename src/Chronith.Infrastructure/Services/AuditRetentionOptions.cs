namespace Chronith.Infrastructure.Services;

public sealed class AuditRetentionOptions
{
    public int RetentionDays { get; set; } = 90;
    public int IntervalHours { get; set; } = 24;
}
