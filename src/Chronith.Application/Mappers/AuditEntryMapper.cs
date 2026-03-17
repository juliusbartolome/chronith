using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class AuditEntryMapper
{
    public static AuditEntryDto ToDto(this AuditEntry entry) =>
        new(
            Id: entry.Id,
            UserId: entry.UserId,
            UserRole: entry.UserRole,
            EntityType: entry.EntityType,
            EntityId: entry.EntityId,
            Action: entry.Action,
            OldValues: entry.OldValues,
            NewValues: entry.NewValues,
            Metadata: entry.Metadata,
            Timestamp: entry.Timestamp);
}
