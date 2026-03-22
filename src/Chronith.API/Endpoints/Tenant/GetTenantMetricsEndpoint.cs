using Chronith.Application.DTOs;
using Chronith.Application.Queries.Tenant.GetTenantMetrics;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

public sealed class GetTenantMetricsEndpoint(ISender sender)
    : EndpointWithoutRequest<TenantMetricsDto>
{
    public override void Configure()
    {
        Get("/tenant/metrics");
        Roles("TenantAdmin", "TenantStaff", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantRead}");
        Options(x => x.WithTags("Tenant").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantMetricsQuery(), ct);
        HttpContext.Response.Headers["Cache-Control"] = "max-age=60, private";
        await Send.OkAsync(result, ct);
    }
}
