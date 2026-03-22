using Chronith.Application.Commands.Subscriptions;
using Chronith.Application.DTOs;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

public sealed class SubscribeRequest
{
    public Guid PlanId { get; set; }
    public string? PaymentMethodToken { get; set; }
}

public sealed class SubscribeEndpoint(ISender sender)
    : Endpoint<SubscribeRequest, TenantSubscriptionDto>
{
    public override void Configure()
    {
        Post("/tenant/subscribe");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantWrite}");
        Options(x => x.WithTags("Subscriptions").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(SubscribeRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new SubscribeCommand
        {
            PlanId = req.PlanId,
            PaymentMethodToken = req.PaymentMethodToken,
        }, ct);
        await Send.ResponseAsync(result, 201, ct);
    }
}
