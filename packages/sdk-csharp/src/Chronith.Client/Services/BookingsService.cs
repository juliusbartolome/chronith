using System.Net.Http.Json;
using Chronith.Client.Models;

namespace Chronith.Client.Services;

public sealed class BookingsService(HttpClient httpClient) : ServiceBase(httpClient)
{
    public async Task<PagedResult<BookingDto>> ListAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var response = await Http.GetAsync(
            $"/v1/bookings?page={page}&pageSize={pageSize}", ct);
        return await ReadJsonAsync<PagedResult<BookingDto>>(response, ct);
    }

    public async Task<BookingDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var response = await Http.GetAsync($"/v1/bookings/{id}", ct);
        return await ReadJsonAsync<BookingDto>(response, ct);
    }

    public async Task<BookingDto> CreateAsync(
        object request,
        CancellationToken ct = default)
    {
        var response = await Http.PostAsJsonAsync("/v1/bookings", request, ct);
        return await ReadJsonAsync<BookingDto>(response, ct);
    }

    public async Task<BookingDto> CancelAsync(
        Guid id,
        string? reason = null,
        CancellationToken ct = default)
    {
        var response = await Http.PostAsJsonAsync(
            $"/v1/bookings/{id}/cancel",
            new { Reason = reason },
            ct);
        return await ReadJsonAsync<BookingDto>(response, ct);
    }

    public async Task<BookingDto> ConfirmAsync(Guid id, CancellationToken ct = default)
    {
        var response = await Http.PostAsync($"/v1/bookings/{id}/confirm", null, ct);
        return await ReadJsonAsync<BookingDto>(response, ct);
    }

    public async Task<BookingDto> RescheduleAsync(
        Guid id,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken ct = default)
    {
        var response = await Http.PostAsJsonAsync(
            $"/v1/bookings/{id}/reschedule",
            new { StartAt = startAt, EndAt = endAt },
            ct);
        return await ReadJsonAsync<BookingDto>(response, ct);
    }
}
