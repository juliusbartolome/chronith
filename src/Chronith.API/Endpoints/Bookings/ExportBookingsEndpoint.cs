using Chronith.Application.Queries.Bookings;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Bookings;

public sealed class ExportBookingsRequest
{
    [QueryParam] public string Format { get; set; } = "csv";
    [QueryParam] public DateTimeOffset? From { get; set; }
    [QueryParam] public DateTimeOffset? To { get; set; }
}

public sealed class ExportBookingsEndpoint(ISender sender) : Endpoint<ExportBookingsRequest>
{
    public override void Configure()
    {
        Get("/bookings/export");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Bookings").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(ExportBookingsRequest req, CancellationToken ct)
    {
        var from = req.From ?? DateTimeOffset.UtcNow.AddMonths(-1);
        var to = req.To ?? DateTimeOffset.UtcNow;

        var result = await sender.Send(
            new ExportBookingsQuery(from, to, req.Format), ct);

        HttpContext.Response.ContentType = result.ContentType;
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{result.FileName}\"";

        await HttpContext.Response.Body.WriteAsync(result.Content, ct);
    }
}
