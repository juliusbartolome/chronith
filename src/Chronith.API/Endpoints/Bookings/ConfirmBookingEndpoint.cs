using Chronith.Application.Commands.Bookings;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Bookings;

public sealed class ConfirmBookingRequest
{
    public Guid BookingId { get; set; }
    public string BookingTypeSlug { get; set; } = string.Empty;
}

public sealed class ConfirmBookingEndpoint(ISender sender)
    : Endpoint<ConfirmBookingRequest, BookingDto>
{
    public override void Configure()
    {
        Post("/bookings/{bookingId}/confirm");
        Roles("TenantAdmin", "TenantStaff");
        Options(x => x.WithTags("Bookings"));
    }

    public override async Task HandleAsync(ConfirmBookingRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new ConfirmBookingCommand
        {
            BookingId = req.BookingId,
            BookingTypeSlug = req.BookingTypeSlug
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
