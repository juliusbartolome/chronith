using Chronith.Application.Commands.Webhooks;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Webhooks;

public sealed class CreateWebhookRequest
{
    // Route param
    public string Slug { get; set; } = string.Empty;

    // Body
    public string Url { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}

public sealed class CreateWebhookEndpoint(ISender sender)
    : Endpoint<CreateWebhookRequest, WebhookDto>
{
    public override void Configure()
    {
        Post("/booking-types/{slug}/webhooks");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Webhooks").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CreateWebhookRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new CreateWebhookCommand
        {
            BookingTypeSlug = req.Slug,
            Url = req.Url,
            Secret = req.Secret
        }, ct);

        await Send.ResponseAsync(result, 201, ct);
    }
}
