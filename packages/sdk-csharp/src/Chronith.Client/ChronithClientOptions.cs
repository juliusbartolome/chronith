namespace Chronith.Client;

/// <summary>
/// Options for configuring the <see cref="ChronithClient"/>.
/// </summary>
public sealed class ChronithClientOptions
{
    /// <summary>
    /// Base URL of the Chronith API (e.g. https://api.example.com).
    /// Do not include a trailing slash.
    /// </summary>
    public required string BaseUrl { get; set; }

    /// <summary>
    /// API key for the X-Api-Key header. Mutually exclusive with <see cref="JwtToken"/>.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Bearer JWT token. Mutually exclusive with <see cref="ApiKey"/>.
    /// </summary>
    public string? JwtToken { get; set; }

    /// <summary>
    /// HTTP request timeout. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of retries on transient failures. Defaults to 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
