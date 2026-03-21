using Chronith.Application.Commands.Bookings;
using Chronith.Application.DTOs;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Bookings;

public sealed class RescheduleBookingRequest
{
    public Guid BookingId { get; set; }
    public DateTimeOffset NewStart { get; set; }
    public DateTimeOffset NewEnd { get; set; }
}

public sealed class RescheduleBookingEndpoint(ISender sender)
    : Endpoint<RescheduleBookingRequest, BookingDto>
{
    public override void Configure()
    {
        Post("/bookings/{bookingId}/reschedule");
        Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.BookingsWrite}");
        Options(x => x.WithTags("Bookings").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(RescheduleBookingRequest req, CancellationToken ct)
    {
        var role = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
        var customerId = role == "Customer" ? User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value : null;

        var result = await sender.Send(new RescheduleBookingCommand
        {
            BookingId = req.BookingId,
            NewStart = req.NewStart,
            NewEnd = req.NewEnd,
            RequiredCustomerId = customerId
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
