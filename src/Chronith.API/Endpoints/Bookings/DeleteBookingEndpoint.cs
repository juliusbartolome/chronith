using Chronith.Application.Commands.Bookings;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Bookings;

public sealed class DeleteBookingRequest
{
    public Guid Id { get; set; }
}

public sealed class DeleteBookingEndpoint(ISender sender)
    : Endpoint<DeleteBookingRequest>
{
    public override void Configure()
    {
        Delete("/bookings/{id}");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Bookings").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(DeleteBookingRequest req, CancellationToken ct)
    {
        await sender.Send(new DeleteBookingCommand(req.Id), ct);
        await Send.NoContentAsync(ct);
    }
}
