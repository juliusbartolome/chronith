namespace Chronith.Application.DTOs;

public sealed record NotificationTemplateDto(
    Guid Id,
    Guid TenantId,
    string EventType,
    string ChannelType,
    string? Subject,
    string Body,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
