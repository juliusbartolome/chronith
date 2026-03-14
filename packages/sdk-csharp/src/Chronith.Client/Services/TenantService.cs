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

    /// <summary>
    /// Updates tenant settings.
    /// </summary>
    /// <param name="request">
    /// An object with any combination of the following optional fields:
    /// <list type="bullet">
    ///   <item><term>LogoUrl</term><description>URL of the tenant logo.</description></item>
    ///   <item><term>PrimaryColor</term><description>Primary brand color (hex).</description></item>
    ///   <item><term>AccentColor</term><description>Accent brand color (hex).</description></item>
    ///   <item><term>CustomDomain</term><description>Custom domain for the booking page.</description></item>
    ///   <item><term>BookingPageEnabled</term><description>Whether the public booking page is enabled.</description></item>
    ///   <item><term>WelcomeMessage</term><description>Welcome message shown to customers.</description></item>
    ///   <item><term>TermsUrl</term><description>URL to the terms and conditions page.</description></item>
    ///   <item><term>PrivacyUrl</term><description>URL to the privacy policy page.</description></item>
    /// </list>
    /// </param>
    /// <param name="ct">Cancellation token.</param>
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
        var response = await Http.GetAsync("/v1/plans", ct);
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
