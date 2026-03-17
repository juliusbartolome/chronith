using Chronith.Application.Commands.Subscriptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

public sealed class CancelSubscriptionRequest
{
    public string? Reason { get; set; }
}

public sealed class CancelSubscriptionEndpoint(ISender sender)
    : Endpoint<CancelSubscriptionRequest>
{
    public override void Configure()
    {
        Delete("/tenant/subscription");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Subscriptions").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancelSubscriptionRequest req, CancellationToken ct)
    {
        await sender.Send(new CancelSubscriptionCommand { Reason = req.Reason }, ct);
        await Send.NoContentAsync(ct);
    }
}
