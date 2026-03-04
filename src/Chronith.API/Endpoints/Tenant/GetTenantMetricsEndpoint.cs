using Chronith.Application.DTOs;
using Chronith.Application.Queries.Tenant.GetTenantMetrics;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

public sealed class GetTenantMetricsEndpoint(ISender sender)
    : EndpointWithoutRequest<TenantMetricsDto>
{
    public override void Configure()
    {
        Get("/tenant/metrics");
        Roles("TenantAdmin", "TenantStaff");
        Options(x => x.WithTags("Tenant"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantMetricsQuery(), ct);
        HttpContext.Response.Headers["Cache-Control"] = "max-age=60, private";
        await Send.OkAsync(result, ct);
    }
}
