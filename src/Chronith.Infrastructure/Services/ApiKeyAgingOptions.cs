namespace Chronith.Infrastructure.Services;

public sealed class ApiKeyAgingOptions
{
    public int ThresholdDays { get; set; } = 90;
    public int CheckIntervalHours { get; set; } = 24;
}
