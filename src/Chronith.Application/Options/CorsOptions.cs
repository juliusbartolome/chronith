namespace Chronith.Application.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Security:Cors";

    public string[] AllowedOrigins { get; set; } = [];
    public bool AllowCredentials { get; set; } = true;
    public string[] AllowedHeaders { get; set; } = ["Authorization", "Content-Type", "X-Api-Key", "Idempotency-Key", "X-Correlation-Id"];
    public string[] ExposedHeaders { get; set; } = ["X-Correlation-Id"];
}
