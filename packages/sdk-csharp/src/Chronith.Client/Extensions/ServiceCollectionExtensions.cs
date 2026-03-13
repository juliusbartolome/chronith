using Microsoft.Extensions.DependencyInjection;

namespace Chronith.Client.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ChronithClient"/> with the DI container.
    /// </summary>
    /// <example>
    /// services.AddChronithClient(options =>
    /// {
    ///     options.BaseUrl = "https://api.example.com";
    ///     options.ApiKey = "your-api-key";
    /// });
    /// </example>
    public static IServiceCollection AddChronithClient(
        this IServiceCollection services,
        Action<ChronithClientOptions> configure)
    {
        var options = new ChronithClientOptions { BaseUrl = string.Empty };
        configure(options);

        services.AddHttpClient<ChronithClient>(client =>
        {
            if (string.IsNullOrWhiteSpace(options.BaseUrl))
                throw new InvalidOperationException(
                    "ChronithClientOptions.BaseUrl must be set.");

            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = options.Timeout;

            if (options.ApiKey is not null)
            {
                client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
            }
            else if (options.JwtToken is not null)
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", options.JwtToken);
            }
        });

        return services;
    }
}
