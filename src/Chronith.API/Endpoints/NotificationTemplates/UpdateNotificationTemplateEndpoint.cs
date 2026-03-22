using Chronith.Application.Commands.NotificationTemplates;
using Chronith.Application.DTOs;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.NotificationTemplates;

public sealed class UpdateNotificationTemplateRequest
{
    // Route param
    public Guid Id { get; set; }

    // Body
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class UpdateNotificationTemplateEndpoint(ISender sender)
    : Endpoint<UpdateNotificationTemplateRequest, NotificationTemplateDto>
{
    public override void Configure()
    {
        Put("/tenant/notification-templates/{id}");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.NotificationTemplatesWrite}");
        Options(x => x.WithTags("NotificationTemplates").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(UpdateNotificationTemplateRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateNotificationTemplateCommand
        {
            Id = req.Id,
            Subject = req.Subject,
            Body = req.Body,
            IsActive = req.IsActive
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
