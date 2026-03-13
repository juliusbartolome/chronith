using Chronith.Client.Models;

namespace Chronith.Client.Services;

public sealed class AuditService(HttpClient httpClient) : ServiceBase(httpClient)
{
    public async Task<PagedResult<object>> ListAsync(
        int page = 1,
        int pageSize = 50,
        string? entityType = null,
        CancellationToken ct = default)
    {
        var url = $"/v1/audit?page={page}&pageSize={pageSize}";
        if (entityType is not null)
            url += $"&entityType={entityType}";

        var response = await Http.GetAsync(url, ct);
        return await ReadJsonAsync<PagedResult<object>>(response, ct);
    }
}
