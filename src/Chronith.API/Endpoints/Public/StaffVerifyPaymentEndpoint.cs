using Chronith.Application.Commands.Public;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public;

public sealed class StaffVerifyPaymentRequest
{
    public string TenantSlug { get; set; } = string.Empty;
    public Guid BookingId { get; set; }

    [QueryParam]
    public long Expires { get; set; }

    [QueryParam]
    public string Sig { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public sealed class StaffVerifyPaymentEndpoint(ISender sender)
    : Endpoint<StaffVerifyPaymentRequest, PublicBookingStatusDto>
{
    public override void Configure()
    {
        Post("/public/{tenantSlug}/bookings/{bookingId}/staff-verify");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Public"));
    }

    public override async Task HandleAsync(StaffVerifyPaymentRequest req, CancellationToken ct)
    {
        var command = new VerifyBookingPaymentCommand
        {
            TenantSlug = req.TenantSlug,
            BookingId = req.BookingId,
            Expires = req.Expires,
            Signature = req.Sig,
            Action = req.Action,
            Note = req.Note
        };

        var result = await sender.Send(command, ct);
        await Send.OkAsync(result, ct);
    }
}
