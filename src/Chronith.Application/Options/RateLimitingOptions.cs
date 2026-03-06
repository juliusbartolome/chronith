namespace Chronith.Application.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int DefaultWindowSeconds { get; init; } = 60;
    public int DefaultPermitLimit { get; init; } = 300;
    public int QueueLimit { get; init; } = 0;
    public Dictionary<string, TenantRateLimitOverride> TenantOverrides { get; init; } = new();
}

public sealed class TenantRateLimitOverride
{
    public int PermitLimit { get; init; }
}
