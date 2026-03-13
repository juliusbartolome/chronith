using System.Net.Http.Json;

namespace Chronith.Client.Services;

public sealed class NotificationsService(HttpClient httpClient) : ServiceBase(httpClient)
{
    public async Task<object> GetChannelConfigAsync(
        string channel,
        CancellationToken ct = default)
    {
        var response = await Http.GetAsync($"/v1/notifications/channels/{channel}", ct);
        return await ReadJsonAsync<object>(response, ct);
    }

    public async Task UpdateChannelConfigAsync(
        string channel,
        object config,
        CancellationToken ct = default)
    {
        var response = await Http.PutAsJsonAsync(
            $"/v1/notifications/channels/{channel}", config, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task SendTestAsync(
        string channel,
        CancellationToken ct = default)
    {
        var response = await Http.PostAsync(
            $"/v1/notifications/channels/{channel}/test", null, ct);
        await EnsureSuccessAsync(response, ct);
    }
}
