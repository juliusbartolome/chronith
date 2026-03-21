using Chronith.Application.Commands.Bookings;
using Chronith.Application.DTOs;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Bookings;

public sealed class PayBookingRequest
{
    public Guid BookingId { get; set; }
}

public sealed class PayBookingEndpoint(ISender sender)
    : Endpoint<PayBookingRequest, BookingDto>
{
    public override void Configure()
    {
        Post("/bookings/{bookingId}/pay");
        Roles("TenantAdmin", "TenantStaff", "TenantPaymentService", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.BookingsPay}");
        Options(x => x.WithTags("Bookings").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(PayBookingRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new PayBookingCommand
        {
            BookingId = req.BookingId,
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
