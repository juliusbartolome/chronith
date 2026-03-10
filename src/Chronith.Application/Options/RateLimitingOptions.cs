namespace Chronith.Application.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public PolicyConfig Authenticated { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };
    public PolicyConfig Public { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };
    public PolicyConfig Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 300 };
    public PolicyConfig Export { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };
    public int QueueLimit { get; set; } = 0;
    public Dictionary<string, TenantOverride> TenantOverrides { get; set; } = [];
}

public sealed class PolicyConfig
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}

public sealed class TenantOverride
{
    public int? PermitLimit { get; set; }
    public int? WindowSeconds { get; set; }
}
