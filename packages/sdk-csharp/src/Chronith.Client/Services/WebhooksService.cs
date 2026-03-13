using System.Net.Http.Json;
using Chronith.Client.Models;

namespace Chronith.Client.Services;

public sealed class WebhooksService(HttpClient httpClient) : ServiceBase(httpClient)
{
    public async Task<PagedResult<WebhookDto>> ListAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var response = await Http.GetAsync(
            $"/v1/webhooks?page={page}&pageSize={pageSize}", ct);
        return await ReadJsonAsync<PagedResult<WebhookDto>>(response, ct);
    }

    public async Task<WebhookDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var response = await Http.GetAsync($"/v1/webhooks/{id}", ct);
        return await ReadJsonAsync<WebhookDto>(response, ct);
    }

    public async Task<WebhookDto> CreateAsync(
        object request,
        CancellationToken ct = default)
    {
        var response = await Http.PostAsJsonAsync("/v1/webhooks", request, ct);
        return await ReadJsonAsync<WebhookDto>(response, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var response = await Http.DeleteAsync($"/v1/webhooks/{id}", ct);
        await EnsureSuccessAsync(response, ct);
    }
}
