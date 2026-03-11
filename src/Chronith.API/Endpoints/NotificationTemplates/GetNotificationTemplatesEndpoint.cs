using Chronith.Application.DTOs;
using Chronith.Application.Queries.NotificationTemplates;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.NotificationTemplates;

public sealed class GetNotificationTemplatesEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<NotificationTemplateDto>>
{
    public override void Configure()
    {
        Get("/tenant/notification-templates");
        Roles("TenantAdmin");
        Options(x => x.WithTags("NotificationTemplates").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetNotificationTemplatesQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
