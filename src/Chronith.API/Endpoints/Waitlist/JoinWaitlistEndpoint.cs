using Chronith.Application.Commands.Waitlist;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Waitlist;

public sealed class JoinWaitlistRequest
{
    public string BookingTypeSlug { get; set; } = string.Empty;
    public DateTimeOffset DesiredStart { get; set; }
    public DateTimeOffset DesiredEnd { get; set; }
}

public sealed class JoinWaitlistEndpoint(ISender sender)
    : Endpoint<JoinWaitlistRequest, WaitlistEntryDto>
{
    public override void Configure()
    {
        Post("/booking-types/{bookingTypeSlug}/waitlist");
        Roles("Customer");
        Options(x => x.WithTags("Waitlist").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(JoinWaitlistRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new JoinWaitlistCommand
        {
            BookingTypeSlug = req.BookingTypeSlug,
            DesiredStart = req.DesiredStart,
            DesiredEnd = req.DesiredEnd
        }, ct);

        await Send.CreatedAtAsync<JoinWaitlistEndpoint>(
            null, result, cancellation: ct);
    }
}
