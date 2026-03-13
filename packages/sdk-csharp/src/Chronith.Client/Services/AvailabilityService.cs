using Chronith.Client.Models;

namespace Chronith.Client.Services;

public sealed class AvailabilityService(HttpClient httpClient) : ServiceBase(httpClient)
{
    public async Task<AvailabilityDto> QueryAsync(
        Guid bookingTypeId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? staffMemberId = null,
        CancellationToken ct = default)
    {
        var url = $"/v1/availability?bookingTypeId={bookingTypeId}&from={from:O}&to={to:O}";
        if (staffMemberId.HasValue)
            url += $"&staffMemberId={staffMemberId.Value}";

        var response = await Http.GetAsync(url, ct);
        return await ReadJsonAsync<AvailabilityDto>(response, ct);
    }
}
