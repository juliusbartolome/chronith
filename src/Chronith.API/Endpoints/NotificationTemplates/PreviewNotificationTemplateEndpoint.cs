using Chronith.Application.Queries.NotificationTemplates;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.NotificationTemplates;

public sealed class PreviewNotificationTemplateRequest
{
    // Route param
    public Guid Id { get; set; }

    // Body — variable substitution tokens
    public Dictionary<string, string> Variables { get; set; } = [];
}

public sealed record PreviewNotificationTemplateResponse(
    string? Subject,
    string Body);

public sealed class PreviewNotificationTemplateEndpoint(ISender sender)
    : Endpoint<PreviewNotificationTemplateRequest, PreviewNotificationTemplateResponse>
{
    public override void Configure()
    {
        Post("/tenant/notification-templates/{id}/preview");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.NotificationTemplatesWrite}");
        Options(x => x.WithTags("NotificationTemplates").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(PreviewNotificationTemplateRequest req, CancellationToken ct)
    {
        var result = await sender.Send(
            new PreviewNotificationTemplateQuery(req.Id, req.Variables), ct);

        await Send.OkAsync(new PreviewNotificationTemplateResponse(result.Subject, result.Body), ct);
    }
}
