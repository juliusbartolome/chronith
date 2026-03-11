using Chronith.Application.DTOs;
using Chronith.Application.Queries.Analytics;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Analytics;

public sealed class GetBookingAnalyticsRequest
{
    [QueryParam]
    public DateTimeOffset From { get; set; }

    [QueryParam]
    public DateTimeOffset To { get; set; }

    [QueryParam]
    public string GroupBy { get; set; } = "day";
}

public sealed class GetBookingAnalyticsEndpoint(ISender sender)
    : Endpoint<GetBookingAnalyticsRequest, BookingAnalyticsDto>
{
    public override void Configure()
    {
        Get("/analytics/bookings");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Analytics").RequireRateLimiting("Export"));
    }

    public override async Task HandleAsync(GetBookingAnalyticsRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new GetBookingAnalyticsQuery(req.From, req.To, req.GroupBy), ct);
        await Send.OkAsync(result, ct);
    }
}
