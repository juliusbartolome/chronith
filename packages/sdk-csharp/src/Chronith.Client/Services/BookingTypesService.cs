using System.Net.Http.Json;
using Chronith.Client.Models;

namespace Chronith.Client.Services;

public sealed class BookingTypesService(HttpClient httpClient) : ServiceBase(httpClient)
{
    public async Task<PagedResult<BookingTypeDto>> ListAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var response = await Http.GetAsync(
            $"/v1/booking-types?page={page}&pageSize={pageSize}", ct);
        return await ReadJsonAsync<PagedResult<BookingTypeDto>>(response, ct);
    }

    public async Task<BookingTypeDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var response = await Http.GetAsync($"/v1/booking-types/{id}", ct);
        return await ReadJsonAsync<BookingTypeDto>(response, ct);
    }

    public async Task<BookingTypeDto> CreateAsync(
        object request,
        CancellationToken ct = default)
    {
        var response = await Http.PostAsJsonAsync("/v1/booking-types", request, ct);
        return await ReadJsonAsync<BookingTypeDto>(response, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var response = await Http.DeleteAsync($"/v1/booking-types/{id}", ct);
        await EnsureSuccessAsync(response, ct);
    }
}
