using Chronith.Application.DTOs;
using Chronith.Application.Queries.Analytics;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Analytics;

public sealed class GetUtilizationAnalyticsRequest
{
    [QueryParam]
    public DateTimeOffset From { get; set; }

    [QueryParam]
    public DateTimeOffset To { get; set; }
}

public sealed class GetUtilizationAnalyticsEndpoint(ISender sender)
    : Endpoint<GetUtilizationAnalyticsRequest, UtilizationAnalyticsDto>
{
    public override void Configure()
    {
        Get("/analytics/utilization");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.AnalyticsRead}");
        Options(x => x.WithTags("Analytics").RequireRateLimiting("Export"));
    }

    public override async Task HandleAsync(GetUtilizationAnalyticsRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new GetUtilizationAnalyticsQuery(req.From, req.To), ct);
        await Send.OkAsync(result, ct);
    }
}
