using Chronith.Application.Commands.Bookings;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Bookings;

public sealed class PayBookingRequest
{
    public Guid BookingId { get; set; }
    public string BookingTypeSlug { get; set; } = string.Empty;
}

public sealed class PayBookingEndpoint(ISender sender)
    : Endpoint<PayBookingRequest, BookingDto>
{
    public override void Configure()
    {
        Post("/bookings/{bookingId}/pay");
        Roles("TenantAdmin", "TenantStaff", "TenantPaymentService");
    }

    public override async Task HandleAsync(PayBookingRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new PayBookingCommand
        {
            BookingId = req.BookingId,
            BookingTypeSlug = req.BookingTypeSlug
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
