using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class WebhookMapper
{
    public static WebhookDto ToDto(this Webhook webhook) =>
        new(
            Id: webhook.Id,
            Url: webhook.Url,
            EventTypes: webhook.EventTypes
        );
}
