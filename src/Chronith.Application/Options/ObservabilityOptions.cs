namespace Chronith.Application.Options;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public string OtlpEndpoint { get; set; } = "http://localhost:4317";
    public string ServiceName { get; set; } = "chronith-api";
    public bool EnableTracing { get; set; } = true;
    public bool EnableMetrics { get; set; } = true;
}
