using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class TenantNotificationConfigMapper
{
    public static TenantNotificationConfigDto ToDto(this TenantNotificationConfig config) =>
        new(
            Id: config.Id,
            ChannelType: config.ChannelType,
            IsEnabled: config.IsEnabled,
            CreatedAt: config.CreatedAt,
            UpdatedAt: config.UpdatedAt);
}
