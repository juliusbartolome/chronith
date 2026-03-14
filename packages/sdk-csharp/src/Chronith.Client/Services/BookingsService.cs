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

    /// <summary>
    /// Creates a new booking.
    /// </summary>
    /// <param name="request">
    /// An object with the following fields:
    /// <list type="bullet">
    ///   <item><term>BookingTypeSlug</term><description>Required. The slug of the booking type.</description></item>
    ///   <item><term>StartTime</term><description>Required. The requested start time (ISO 8601).</description></item>
    ///   <item><term>CustomerEmail</term><description>Required. The customer's email address.</description></item>
    ///   <item><term>CustomerId</term><description>Optional. The customer's identifier.</description></item>
    /// </list>
    /// </param>
    /// <param name="ct">Cancellation token.</param>
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
