using Chronith.Application.DTOs;
using Chronith.Application.Queries.Recurring;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Recurring;

public sealed class ListRecurrenceRulesEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<RecurrenceRuleDto>>
{
    public override void Configure()
    {
        Get("/recurring");
        Roles("TenantAdmin", "TenantStaff");
        Options(x => x.WithTags("Recurring"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new ListRecurrenceRulesQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
