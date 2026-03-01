namespace Chronith.Application.DTOs;

public sealed record WebhookDto(
    Guid Id,
    string Url
);
