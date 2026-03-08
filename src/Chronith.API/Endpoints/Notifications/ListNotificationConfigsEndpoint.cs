using Chronith.Application.DTOs;
using Chronith.Application.Queries.Notifications;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Notifications;

public sealed class ListNotificationConfigsEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<TenantNotificationConfigDto>>
{
    public override void Configure()
    {
        Get("/tenant/notifications");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Notifications"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new ListNotificationConfigsQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
