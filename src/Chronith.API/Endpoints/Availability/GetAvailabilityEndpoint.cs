using Chronith.Application.DTOs;
using Chronith.Application.Queries.Availability;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Availability;

public sealed class GetAvailabilityRequest
{
    public string Slug { get; set; } = string.Empty;

    [QueryParam]
    public DateTimeOffset From { get; set; }

    [QueryParam]
    public DateTimeOffset To { get; set; }
}

public sealed class GetAvailabilityEndpoint(ISender sender)
    : Endpoint<GetAvailabilityRequest, AvailabilityDto>
{
    public override void Configure()
    {
        Get("/booking-types/{slug}/availability");
        Roles("TenantAdmin", "TenantStaff", "Customer");
    }

    public override async Task HandleAsync(GetAvailabilityRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new GetAvailabilityQuery
        {
            BookingTypeSlug = req.Slug,
            From = req.From,
            To = req.To
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
