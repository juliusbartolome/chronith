using Chronith.Application.DTOs;
using Chronith.Application.Queries.BookingTypes;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.BookingTypes;

public sealed class ListBookingTypesEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<BookingTypeDto>>
{
    public override void Configure()
    {
        Get("/booking-types");
        Roles("TenantAdmin", "TenantStaff", "Customer");
        Options(x => x.WithTags("BookingTypes"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new ListBookingTypesQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
