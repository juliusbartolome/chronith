using Chronith.Application.DTOs;
using Chronith.Application.Queries.TenantAuthConfig.GetTenantAuthConfig;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

public sealed class GetTenantAuthConfigEndpoint(ISender sender)
    : EndpointWithoutRequest<TenantAuthConfigDto?>
{
    public override void Configure()
    {
        Get("/tenant/auth-config");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Tenant").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantAuthConfigQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
