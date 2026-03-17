namespace Chronith.Application.DTOs;

public sealed record TenantNotificationConfigDto(
    Guid Id,
    string ChannelType,
    bool IsEnabled,
    string Settings,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
