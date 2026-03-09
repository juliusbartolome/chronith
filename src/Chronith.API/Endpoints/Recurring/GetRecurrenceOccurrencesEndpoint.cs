using Chronith.Application.Queries.Recurring;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Recurring;

public sealed class GetRecurrenceOccurrencesRequest
{
    public Guid Id { get; set; }

    [QueryParam]
    public DateOnly From { get; set; }

    [QueryParam]
    public DateOnly To { get; set; }
}

public sealed class GetRecurrenceOccurrencesEndpoint(ISender sender)
    : Endpoint<GetRecurrenceOccurrencesRequest, IReadOnlyList<DateOnly>>
{
    public override void Configure()
    {
        Get("/recurring/{id}/occurrences");
        Roles("TenantAdmin", "TenantStaff", "Customer");
        Options(x => x.WithTags("Recurring"));
    }

    public override async Task HandleAsync(GetRecurrenceOccurrencesRequest req, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetRecurrenceOccurrencesQuery(req.Id, req.From, req.To), ct);
        await Send.OkAsync(result, ct);
    }
}
