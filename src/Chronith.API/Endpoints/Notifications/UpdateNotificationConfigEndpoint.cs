using Chronith.Application.Commands.NotificationConfig;
using Chronith.Application.DTOs;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Notifications;

public sealed class UpdateNotificationConfigRequest
{
    public string ChannelType { get; set; } = string.Empty;
    public string Settings { get; set; } = string.Empty;
}

public sealed class UpdateNotificationConfigEndpoint(ISender sender)
    : Endpoint<UpdateNotificationConfigRequest, TenantNotificationConfigDto>
{
    public override void Configure()
    {
        Put("/tenant/notifications/{channelType}");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.NotificationsWrite}");
        Options(x => x.WithTags("Notifications").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(UpdateNotificationConfigRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateNotificationConfigCommand
        {
            ChannelType = req.ChannelType,
            Settings = req.Settings
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
