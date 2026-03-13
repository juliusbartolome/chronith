using Chronith.Client.Models;

namespace Chronith.Client.Services;

public sealed class AnalyticsService(HttpClient httpClient) : ServiceBase(httpClient)
{
    public async Task<AnalyticsBookingsDto> GetBookingsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string groupBy = "day",
        CancellationToken ct = default)
    {
        var response = await Http.GetAsync(
            $"/v1/analytics/bookings?from={from:O}&to={to:O}&groupBy={groupBy}", ct);
        return await ReadJsonAsync<AnalyticsBookingsDto>(response, ct);
    }
}
