using Chronith.Application.DTOs;
using Chronith.Application.Queries.BookingTypes;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.BookingTypes;

public sealed class GetBookingTypeRequest
{
    public string Slug { get; set; } = string.Empty;
}

public sealed class GetBookingTypeEndpoint(ISender sender)
    : Endpoint<GetBookingTypeRequest, BookingTypeDto>
{
    public override void Configure()
    {
        Get("/booking-types/{slug}");
        Roles("TenantAdmin", "TenantStaff", "Customer");
        Options(x => x.WithTags("BookingTypes"));
    }

    public override async Task HandleAsync(GetBookingTypeRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new GetBookingTypeQuery(req.Slug), ct);
        await Send.OkAsync(result, ct);
    }
}
