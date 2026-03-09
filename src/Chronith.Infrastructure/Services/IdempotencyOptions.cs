namespace Chronith.Infrastructure.Services;

public sealed class IdempotencyOptions
{
    public int CleanupIntervalHours { get; set; } = 6;
    public int ExpirationHours { get; set; } = 24;
}
