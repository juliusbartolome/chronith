using Chronith.Application.DTOs;
using Chronith.Application.Queries.Bookings;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Bookings;

public sealed class GetBookingRequest
{
    public Guid BookingId { get; set; }
}

public sealed class GetBookingEndpoint(ISender sender)
    : Endpoint<GetBookingRequest, BookingDto>
{
    public override void Configure()
    {
        Get("/bookings/{bookingId}");
        Roles("TenantAdmin", "TenantStaff", "Customer");
    }

    public override async Task HandleAsync(GetBookingRequest req, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetBookingQuery(string.Empty, req.BookingId), ct);
        await Send.OkAsync(result, ct);
    }
}
