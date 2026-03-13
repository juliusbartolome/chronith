using Chronith.Application.Commands.Subscriptions;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

public sealed class ChangePlanRequest
{
    public Guid NewPlanId { get; set; }
}

public sealed class ChangePlanEndpoint(ISender sender)
    : Endpoint<ChangePlanRequest, TenantSubscriptionDto>
{
    public override void Configure()
    {
        Put("/tenant/subscription/plan");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Subscriptions").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(ChangePlanRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new ChangePlanCommand { NewPlanId = req.NewPlanId }, ct);
        await Send.OkAsync(result, ct);
    }
}
