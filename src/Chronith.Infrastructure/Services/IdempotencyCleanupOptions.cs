namespace Chronith.Infrastructure.Services;

public sealed class IdempotencyCleanupOptions
{
    public int CleanupIntervalHours { get; set; } = 6;
}
