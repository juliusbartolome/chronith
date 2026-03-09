using Chronith.Application.Commands.Recurring.CancelRecurrenceRule;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Recurring;

public sealed class CancelRecurrenceRuleRequest
{
    public Guid Id { get; set; }
}

public sealed class CancelRecurrenceRuleEndpoint(ISender sender)
    : Endpoint<CancelRecurrenceRuleRequest>
{
    public override void Configure()
    {
        Delete("/recurring/{id}");
        Roles("TenantAdmin", "TenantStaff", "Customer");
        Options(x => x.WithTags("Recurring"));
    }

    public override async Task HandleAsync(CancelRecurrenceRuleRequest req, CancellationToken ct)
    {
        await sender.Send(new CancelRecurrenceRuleCommand
        {
            Id = req.Id
        }, ct);

        await Send.NoContentAsync(ct);
    }
}
