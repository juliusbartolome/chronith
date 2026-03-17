using System.Net.Http.Json;
using Chronith.Client.Models;

namespace Chronith.Client.Services;

public sealed class BookingTypesService(HttpClient httpClient) : ServiceBase(httpClient)
{
    public async Task<IReadOnlyList<BookingTypeDto>> ListAsync(
        CancellationToken ct = default)
    {
        var response = await Http.GetAsync("/v1/booking-types", ct);
        return await ReadJsonAsync<IReadOnlyList<BookingTypeDto>>(response, ct);
    }

    public async Task<BookingTypeDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var response = await Http.GetAsync($"/v1/booking-types/{id}", ct);
        return await ReadJsonAsync<BookingTypeDto>(response, ct);
    }

    /// <summary>
    /// Creates a new booking type.
    /// </summary>
    /// <param name="request">
    /// An object with the following fields:
    /// <list type="bullet">
    ///   <item><term>Slug</term><description>Required. URL-safe identifier.</description></item>
    ///   <item><term>Name</term><description>Required. Display name.</description></item>
    ///   <item><term>IsTimeSlot</term><description>Required. True for time-slot; false for calendar.</description></item>
    ///   <item><term>Capacity</term><description>Required. Max simultaneous bookings.</description></item>
    ///   <item><term>PaymentMode</term><description>Required. E.g. "None", "Online", "OnSite".</description></item>
    ///   <item><term>PaymentProvider</term><description>Optional. E.g. "PayMongo".</description></item>
    ///   <item><term>PriceInCentavos</term><description>Required. Price in centavos (0 for free).</description></item>
    ///   <item><term>Currency</term><description>Required. Currency code, e.g. "PHP".</description></item>
    ///   <item><term>RequiresStaffAssignment</term><description>Required. Whether a staff member must be assigned.</description></item>
    ///   <item><term>DurationMinutes</term><description>Required. Duration in minutes.</description></item>
    ///   <item><term>BufferBeforeMinutes</term><description>Optional. Buffer before each slot.</description></item>
    ///   <item><term>BufferAfterMinutes</term><description>Optional. Buffer after each slot.</description></item>
    ///   <item><term>AvailabilityWindows</term><description>Optional. Time-slot availability windows.</description></item>
    ///   <item><term>AvailableDays</term><description>Optional. Days of week open for calendar type.</description></item>
    /// </list>
    /// </param>
    /// <param name="ct">Cancellation token.</param>
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
