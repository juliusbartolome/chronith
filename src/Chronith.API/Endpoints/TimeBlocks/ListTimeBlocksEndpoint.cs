using Chronith.Application.DTOs;
using Chronith.Application.Queries.TimeBlocks;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.TimeBlocks;

public sealed class ListTimeBlocksRequest
{
    [QueryParam]
    public DateTimeOffset From { get; set; }

    [QueryParam]
    public DateTimeOffset To { get; set; }

    [QueryParam]
    public Guid? BookingTypeId { get; set; }

    [QueryParam]
    public Guid? StaffMemberId { get; set; }
}

public sealed class ListTimeBlocksEndpoint(ISender sender)
    : Endpoint<ListTimeBlocksRequest, IReadOnlyList<TimeBlockDto>>
{
    public override void Configure()
    {
        Get("/time-blocks");
        Roles("TenantAdmin", "TenantStaff");
        Options(x => x.WithTags("TimeBlocks"));
    }

    public override async Task HandleAsync(ListTimeBlocksRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new ListTimeBlocksQuery(
            req.From, req.To, req.BookingTypeId, req.StaffMemberId), ct);

        await Send.OkAsync(result, ct);
    }
}
