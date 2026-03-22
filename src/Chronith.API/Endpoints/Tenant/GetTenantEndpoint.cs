using Chronith.Application.DTOs;
using Chronith.Application.Queries.Tenant;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

public sealed class GetTenantEndpoint(ISender sender)
    : EndpointWithoutRequest<TenantDto>
{
    public override void Configure()
    {
        Get("/tenant");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantRead}");
        Options(x => x.WithTags("Tenant").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
