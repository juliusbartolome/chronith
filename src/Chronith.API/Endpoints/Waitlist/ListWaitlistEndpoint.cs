using Chronith.Application.DTOs;
using Chronith.Application.Queries.Waitlist;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Waitlist;

public sealed class ListWaitlistRequest
{
    public string BookingTypeSlug { get; set; } = string.Empty;

    [QueryParam]
    public DateTimeOffset From { get; set; }

    [QueryParam]
    public DateTimeOffset To { get; set; }
}

public sealed class ListWaitlistEndpoint(ISender sender)
    : Endpoint<ListWaitlistRequest, IReadOnlyList<WaitlistEntryDto>>
{
    public override void Configure()
    {
        Get("/booking-types/{bookingTypeSlug}/waitlist");
        Roles("TenantAdmin", "TenantStaff", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.BookingsRead}");
        Options(x => x.WithTags("Waitlist").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(ListWaitlistRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new ListWaitlistQuery(
            req.BookingTypeSlug, req.From, req.To), ct);

        await Send.OkAsync(result, ct);
    }
}
