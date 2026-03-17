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

    /// <summary>
    /// Registers a new webhook. The response contains only <c>Id</c> and <c>Url</c>.
    /// </summary>
    /// <param name="request">
    /// An object with the following fields:
    /// <list type="bullet">
    ///   <item><term>BookingTypeSlug</term><description>Required. Scopes the webhook to a specific booking type.</description></item>
    ///   <item><term>Url</term><description>Required. HTTPS URL that will receive webhook POST requests.</description></item>
    ///   <item><term>Secret</term><description>Required. Shared secret used to sign the webhook payload.</description></item>
    /// </list>
    /// </param>
    /// <param name="ct">Cancellation token.</param>
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
