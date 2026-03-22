using Chronith.Application.Queries.Audit;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Audit;

public sealed class ExportAuditRequest
{
    [QueryParam] public DateTimeOffset? From { get; set; }
    [QueryParam] public DateTimeOffset? To { get; set; }
}

public sealed class ExportAuditEndpoint(ISender sender) : Endpoint<ExportAuditRequest>
{
    public override void Configure()
    {
        Get("/audit/export");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.AuditRead}");
        Options(x => x.WithTags("Audit").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(ExportAuditRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new ExportAuditQuery(req.From, req.To), ct);

        HttpContext.Response.ContentType = result.ContentType;
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{result.FileName}\"";

        await HttpContext.Response.Body.WriteAsync(result.Content, ct);
    }
}
