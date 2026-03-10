using Chronith.Application.Commands.NotificationTemplates;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.NotificationTemplates;

public sealed class ResetNotificationTemplateRequest
{
    // Route param
    public string EventType { get; set; } = string.Empty;
}

public sealed class ResetNotificationTemplateEndpoint(ISender sender)
    : Endpoint<ResetNotificationTemplateRequest>
{
    public override void Configure()
    {
        Post("/tenant/notification-templates/reset/{eventType}");
        Roles("TenantAdmin");
        Options(x => x.WithTags("NotificationTemplates").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(ResetNotificationTemplateRequest req, CancellationToken ct)
    {
        await sender.Send(new ResetNotificationTemplateCommand
        {
            EventType = req.EventType
        }, ct);

        await Send.NoContentAsync(ct);
    }
}
