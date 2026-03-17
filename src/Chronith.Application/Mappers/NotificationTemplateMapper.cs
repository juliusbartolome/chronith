using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class NotificationTemplateMapper
{
    public static NotificationTemplateDto ToDto(this NotificationTemplate template) => new(
        Id: template.Id,
        TenantId: template.TenantId,
        EventType: template.EventType,
        ChannelType: template.ChannelType,
        Subject: template.Subject,
        Body: template.Body,
        IsActive: template.IsActive,
        CreatedAt: template.CreatedAt,
        UpdatedAt: template.UpdatedAt
    );
}
