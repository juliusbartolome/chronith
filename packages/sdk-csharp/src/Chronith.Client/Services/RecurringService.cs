using System.Net.Http.Json;
using Chronith.Client.Models;

namespace Chronith.Client.Services;

public sealed class RecurringService(HttpClient httpClient) : ServiceBase(httpClient)
{
    public async Task<PagedResult<BookingDto>> ListSeriesAsync(
        Guid seriesId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var response = await Http.GetAsync(
            $"/v1/bookings/series/{seriesId}?page={page}&pageSize={pageSize}", ct);
        return await ReadJsonAsync<PagedResult<BookingDto>>(response, ct);
    }

    public async Task CancelSeriesAsync(
        Guid seriesId,
        string? reason = null,
        CancellationToken ct = default)
    {
        var response = await Http.PostAsJsonAsync(
            $"/v1/bookings/series/{seriesId}/cancel",
            new { Reason = reason },
            ct);
        await EnsureSuccessAsync(response, ct);
    }
}
