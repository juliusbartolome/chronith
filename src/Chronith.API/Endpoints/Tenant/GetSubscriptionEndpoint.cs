using Chronith.Application.DTOs;
using Chronith.Application.Queries.Subscriptions;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

public sealed class GetSubscriptionEndpoint(ISender sender)
    : EndpointWithoutRequest<TenantSubscriptionDto>
{
    public override void Configure()
    {
        Get("/tenant/subscription");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantRead}");
        Options(x => x.WithTags("Subscriptions").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetCurrentSubscriptionQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
