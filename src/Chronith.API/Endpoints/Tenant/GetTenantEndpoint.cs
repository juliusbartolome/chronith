using Chronith.Application.DTOs;
using Chronith.Application.Queries.Tenant;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

public sealed class GetTenantEndpoint(ISender sender)
    : EndpointWithoutRequest<TenantDto>
{
    public override void Configure()
    {
        Get("/tenant");
        Roles("TenantAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
