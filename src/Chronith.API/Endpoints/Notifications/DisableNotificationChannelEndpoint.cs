using Chronith.Application.Commands.NotificationConfig;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Notifications;

public sealed class DisableNotificationChannelRequest
{
    public string ChannelType { get; set; } = string.Empty;
}

public sealed class DisableNotificationChannelEndpoint(ISender sender)
    : Endpoint<DisableNotificationChannelRequest>
{
    public override void Configure()
    {
        Delete("/tenant/notifications/{channelType}");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.NotificationsWrite}");
        Options(x => x.WithTags("Notifications").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(DisableNotificationChannelRequest req, CancellationToken ct)
    {
        await sender.Send(new DisableNotificationChannelCommand
        {
            ChannelType = req.ChannelType
        }, ct);

        await Send.NoContentAsync(ct);
    }
}
