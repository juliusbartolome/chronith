namespace Chronith.Application.DTOs;

public sealed record TenantNotificationConfigDto(
    Guid Id,
    string ChannelType,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
