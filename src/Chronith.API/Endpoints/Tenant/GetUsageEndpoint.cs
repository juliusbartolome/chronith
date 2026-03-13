using Chronith.Application.DTOs;
using Chronith.Application.Queries.Subscriptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

public sealed class GetUsageEndpoint(ISender sender)
    : EndpointWithoutRequest<TenantUsageDto>
{
    public override void Configure()
    {
        Get("/tenant/usage");
        Roles("TenantAdmin", "TenantStaff");
        Options(x => x.WithTags("Subscriptions").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetUsageQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
