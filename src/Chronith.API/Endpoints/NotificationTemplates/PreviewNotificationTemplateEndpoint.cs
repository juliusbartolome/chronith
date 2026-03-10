using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FastEndpoints;

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

public sealed class PreviewNotificationTemplateEndpoint(
    ITenantContext tenantContext,
    INotificationTemplateRepository templateRepo)
    : Endpoint<PreviewNotificationTemplateRequest, PreviewNotificationTemplateResponse>
{
    public override void Configure()
    {
        Post("/tenant/notification-templates/{id}/preview");
        Roles("TenantAdmin");
        Options(x => x.WithTags("NotificationTemplates").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(PreviewNotificationTemplateRequest req, CancellationToken ct)
    {
        var template = await templateRepo.GetByIdAsync(tenantContext.TenantId, req.Id, ct)
            ?? throw new NotFoundException("NotificationTemplate", req.Id);

        var body = Substitute(template.Body, req.Variables);
        var subject = template.Subject is not null
            ? Substitute(template.Subject, req.Variables)
            : null;

        await Send.OkAsync(new PreviewNotificationTemplateResponse(subject, body), ct);
    }

    private static string Substitute(string text, Dictionary<string, string> variables)
    {
        foreach (var (key, value) in variables)
            text = text.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        return text;
    }
}
