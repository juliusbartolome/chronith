using Chronith.Application.DTOs;
using Chronith.Application.Queries.Bookings;
using Chronith.Domain.Models;
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
        Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.BookingsRead}");
        Options(x => x.WithTags("Bookings").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(GetBookingRequest req, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetBookingQuery(string.Empty, req.BookingId), ct);
        await Send.OkAsync(result, ct);
    }
}
