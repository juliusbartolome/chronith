using Chronith.Application.DTOs;
using Chronith.Application.Queries.Notifications;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Notifications;

public sealed class ListNotificationConfigsEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<TenantNotificationConfigDto>>
{
    public override void Configure()
    {
        Get("/tenant/notifications");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.NotificationsWrite}");
        Options(x => x.WithTags("Notifications").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new ListNotificationConfigsQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
