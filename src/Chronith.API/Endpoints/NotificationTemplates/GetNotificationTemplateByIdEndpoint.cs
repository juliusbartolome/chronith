using Chronith.Application.DTOs;
using Chronith.Application.Queries.NotificationTemplates;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.NotificationTemplates;

public sealed class GetNotificationTemplateByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetNotificationTemplateByIdEndpoint(ISender sender)
    : Endpoint<GetNotificationTemplateByIdRequest, NotificationTemplateDto>
{
    public override void Configure()
    {
        Get("/tenant/notification-templates/{id}");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.NotificationTemplatesWrite}");
        Options(x => x.WithTags("NotificationTemplates").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(GetNotificationTemplateByIdRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new GetNotificationTemplateByIdQuery(req.Id), ct);
        await Send.OkAsync(result, ct);
    }
}
