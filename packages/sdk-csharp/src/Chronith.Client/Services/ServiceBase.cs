using System.Net.Http.Json;
using Chronith.Client.Errors;

namespace Chronith.Client.Services;

/// <summary>
/// Base class providing shared JSON serialization helpers for all service classes.
/// </summary>
public abstract class ServiceBase(HttpClient httpClient)
{
    protected HttpClient Http { get; } = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    protected static async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new ChronithApiException(response.StatusCode, body);
        }

        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct))!;
    }

    protected static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new ChronithApiException(response.StatusCode, body);
        }
    }
}
