using Chronith.Application.DTOs;
using Chronith.Application.Queries.Recurring;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Recurring;

public sealed class GetRecurrenceRuleRequest
{
    public Guid Id { get; set; }
}

public sealed class GetRecurrenceRuleEndpoint(ISender sender)
    : Endpoint<GetRecurrenceRuleRequest, RecurrenceRuleDto>
{
    public override void Configure()
    {
        Get("/recurring/{id}");
        Roles("TenantAdmin", "TenantStaff", "Customer");
        Options(x => x.WithTags("Recurring"));
    }

    public override async Task HandleAsync(GetRecurrenceRuleRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new GetRecurrenceRuleQuery(req.Id), ct);
        await Send.OkAsync(result, ct);
    }
}
