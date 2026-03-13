using System.Net.Http.Json;
using Chronith.Client.Models;

namespace Chronith.Client.Services;

public sealed class TenantService(HttpClient httpClient) : ServiceBase(httpClient)
{
    public async Task<TenantSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var response = await Http.GetAsync("/v1/tenant/settings", ct);
        return await ReadJsonAsync<TenantSettingsDto>(response, ct);
    }

    public async Task<TenantSettingsDto> UpdateSettingsAsync(
        object request,
        CancellationToken ct = default)
    {
        var response = await Http.PutAsJsonAsync("/v1/tenant/settings", request, ct);
        return await ReadJsonAsync<TenantSettingsDto>(response, ct);
    }

    public async Task<IReadOnlyList<TenantPlanDto>> GetPlansAsync(
        CancellationToken ct = default)
    {
        var response = await Http.GetAsync("/v1/tenant/plans", ct);
        return await ReadJsonAsync<IReadOnlyList<TenantPlanDto>>(response, ct);
    }

    public async Task<TenantSubscriptionDto> GetSubscriptionAsync(
        CancellationToken ct = default)
    {
        var response = await Http.GetAsync("/v1/tenant/subscription", ct);
        return await ReadJsonAsync<TenantSubscriptionDto>(response, ct);
    }

    public async Task<TenantUsageDto> GetUsageAsync(CancellationToken ct = default)
    {
        var response = await Http.GetAsync("/v1/tenant/usage", ct);
        return await ReadJsonAsync<TenantUsageDto>(response, ct);
    }
}
