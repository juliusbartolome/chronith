using Chronith.Application.DTOs;
using Chronith.Application.Queries.TenantSettings;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

public sealed class GetTenantSettingsEndpoint(ISender sender)
    : EndpointWithoutRequest<TenantSettingsDto>
{
    public override void Configure()
    {
        Get("/tenant/settings");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantRead}");
        Options(x => x.WithTags("Tenant").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantSettingsQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
