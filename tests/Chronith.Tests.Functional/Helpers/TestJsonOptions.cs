using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chronith.Tests.Functional.Helpers;

public static class TestJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<T?> ReadFromApiJsonAsync<T>(this HttpResponseMessage response, CancellationToken ct = default)
        => await response.Content.ReadFromJsonAsync<T>(Default, ct);
}
