using Chronith.Application.Queries.Analytics;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Analytics;

public sealed class ExportAnalyticsRequest
{
    [QueryParam] public string Format { get; set; } = "csv";
    [QueryParam] public DateTimeOffset? From { get; set; }
    [QueryParam] public DateTimeOffset? To { get; set; }
    [QueryParam] public string GroupBy { get; set; } = "day";
}

public sealed class ExportAnalyticsEndpoint(ISender sender) : Endpoint<ExportAnalyticsRequest>
{
    public override void Configure()
    {
        Get("/analytics/bookings/export");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.AnalyticsRead}");
        Options(x => x.WithTags("Analytics").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(ExportAnalyticsRequest req, CancellationToken ct)
    {
        var from = req.From ?? DateTimeOffset.UtcNow.AddMonths(-1);
        var to = req.To ?? DateTimeOffset.UtcNow;

        var result = await sender.Send(
            new ExportAnalyticsQuery(from, to, req.GroupBy, req.Format), ct);

        HttpContext.Response.ContentType = result.ContentType;
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{result.FileName}\"";

        await HttpContext.Response.Body.WriteAsync(result.Content, ct);
    }
}
