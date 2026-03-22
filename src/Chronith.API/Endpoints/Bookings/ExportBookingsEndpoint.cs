using Chronith.Application.Queries.Bookings;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Bookings;

public sealed class ExportBookingsRequest
{
    [QueryParam] public string Format { get; set; } = "csv";
    [QueryParam] public DateTimeOffset? From { get; set; }
    [QueryParam] public DateTimeOffset? To { get; set; }
    [QueryParam] public string? Status { get; set; }
    [QueryParam] public string? BookingTypeSlug { get; set; }
    [QueryParam] public Guid? StaffMemberId { get; set; }
}

public sealed class ExportBookingsEndpoint(ISender sender) : Endpoint<ExportBookingsRequest>
{
    public override void Configure()
    {
        Get("/bookings/export");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.BookingsRead}");
        Options(x => x.WithTags("Bookings").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(ExportBookingsRequest req, CancellationToken ct)
    {
        var from = req.From ?? DateTimeOffset.UtcNow.AddMonths(-1);
        var to = req.To ?? DateTimeOffset.UtcNow;

        var result = await sender.Send(
            new ExportBookingsQuery(from, to, req.Format, req.Status, req.BookingTypeSlug, req.StaffMemberId), ct);

        if (result.RowCount == 10_000)
            HttpContext.Response.Headers["X-Export-Truncated"] = "true";

        HttpContext.Response.ContentType = result.ContentType;
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{result.FileName}\"";

        await HttpContext.Response.Body.WriteAsync(result.Content, ct);
    }
}
