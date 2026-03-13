using System.Net.Http.Json;
using Chronith.Client.Models;

namespace Chronith.Client.Services;

public sealed class StaffService(HttpClient httpClient) : ServiceBase(httpClient)
{
    public async Task<PagedResult<StaffMemberDto>> ListAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var response = await Http.GetAsync(
            $"/v1/staff?page={page}&pageSize={pageSize}", ct);
        return await ReadJsonAsync<PagedResult<StaffMemberDto>>(response, ct);
    }

    public async Task<StaffMemberDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var response = await Http.GetAsync($"/v1/staff/{id}", ct);
        return await ReadJsonAsync<StaffMemberDto>(response, ct);
    }

    /// <summary>
    /// Creates a new staff member.
    /// </summary>
    /// <param name="request">
    /// An object with the following fields:
    /// <list type="bullet">
    ///   <item><term>Name</term><description>Required. The staff member's display name.</description></item>
    ///   <item><term>Email</term><description>Required. The staff member's email address.</description></item>
    ///   <item><term>TenantUserId</term><description>Optional. Links the staff member to a tenant user account.</description></item>
    ///   <item><term>AvailabilityWindows</term><description>Required. Collection of availability windows for scheduling.</description></item>
    /// </list>
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<StaffMemberDto> CreateAsync(
        object request,
        CancellationToken ct = default)
    {
        var response = await Http.PostAsJsonAsync("/v1/staff", request, ct);
        return await ReadJsonAsync<StaffMemberDto>(response, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var response = await Http.DeleteAsync($"/v1/staff/{id}", ct);
        await EnsureSuccessAsync(response, ct);
    }
}
