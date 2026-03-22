using Chronith.Application.Commands.Bookings;
using Chronith.Application.DTOs;
using Chronith.API.Endpoints.Bookings;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Bookings;

public sealed class CreateBookingRequest
{
    // Route param
    public string Slug { get; set; } = string.Empty;

    // Body
    public DateTimeOffset StartTime { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
}

public sealed class CreateBookingEndpoint(ISender sender)
    : Endpoint<CreateBookingRequest, BookingDto>
{
    public override void Configure()
    {
        Post("/booking-types/{slug}/bookings");
        Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.BookingsWrite}");
        Options(x => x.WithTags("Bookings").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CreateBookingRequest req, CancellationToken ct)
    {
        var command = new CreateBookingCommand
        {
            BookingTypeSlug = req.Slug,
            StartTime = req.StartTime,
            CustomerEmail = req.CustomerEmail
        };

        var result = await sender.Send(command, ct);
        await Send.CreatedAtAsync<GetBookingEndpoint>(
            new { bookingId = result.Id }, result, cancellation: ct);
    }
}
