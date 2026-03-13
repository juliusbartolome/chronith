using Chronith.Application.DTOs;
using Chronith.Application.Queries.Plans;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Plans;

public sealed class GetPlansEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<TenantPlanDto>>
{
    public override void Configure()
    {
        Get("/plans");
        AllowAnonymous();
        Options(x => x.WithTags("Plans"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetPlansQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
