using Chronith.Application.Commands.Bookings;
using Chronith.Application.DTOs;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Bookings;

public sealed class CancelBookingRequest
{
    public Guid BookingId { get; set; }
    public string BookingTypeSlug { get; set; } = string.Empty;
}

public sealed class CancelBookingEndpoint(ISender sender)
    : Endpoint<CancelBookingRequest, BookingDto>
{
    public override void Configure()
    {
        Post("/bookings/{bookingId}/cancel");
        Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.BookingsCancel}");
        Options(x => x.WithTags("Bookings").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancelBookingRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new CancelBookingCommand
        {
            BookingId = req.BookingId,
            BookingTypeSlug = req.BookingTypeSlug
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
