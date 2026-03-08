using Chronith.Application.Queries.Integrations;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Integrations;

public sealed class ICalFeedEndpoint(ISender sender)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/booking-types/{slug}/calendar.ics");
        AllowAnonymous();
        Options(x => x.WithTags("Integrations"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug")!;
        var ical = await sender.Send(new GetICalFeedQuery(slug), ct);

        HttpContext.Response.ContentType = "text/calendar";
        await HttpContext.Response.WriteAsync(ical, ct);
    }
}
