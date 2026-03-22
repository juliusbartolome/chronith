using Chronith.Application.DTOs;
using Chronith.Application.Queries.Analytics;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Analytics;

public sealed class GetRevenueAnalyticsRequest
{
    [QueryParam]
    public DateTimeOffset From { get; set; }

    [QueryParam]
    public DateTimeOffset To { get; set; }

    [QueryParam]
    public string GroupBy { get; set; } = "day";
}

public sealed class GetRevenueAnalyticsEndpoint(ISender sender)
    : Endpoint<GetRevenueAnalyticsRequest, RevenueAnalyticsDto>
{
    public override void Configure()
    {
        Get("/analytics/revenue");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.AnalyticsRead}");
        Options(x => x.WithTags("Analytics").RequireRateLimiting("Export"));
    }

    public override async Task HandleAsync(GetRevenueAnalyticsRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new GetRevenueAnalyticsQuery(req.From, req.To, req.GroupBy), ct);
        await Send.OkAsync(result, ct);
    }
}
