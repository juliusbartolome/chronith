using Chronith.Application.Commands.BookingTypes;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.BookingTypes;

public sealed class DeleteBookingTypeRequest
{
    public string Slug { get; set; } = string.Empty;
}

public sealed class DeleteBookingTypeEndpoint(ISender sender)
    : Endpoint<DeleteBookingTypeRequest>
{
    public override void Configure()
    {
        Delete("/booking-types/{slug}");
        Roles("TenantAdmin");
    }

    public override async Task HandleAsync(DeleteBookingTypeRequest req, CancellationToken ct)
    {
        await sender.Send(new DeleteBookingTypeCommand(req.Slug), ct);
        await Send.NoContentAsync(ct);
    }
}
