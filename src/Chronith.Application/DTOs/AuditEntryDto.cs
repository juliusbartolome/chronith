namespace Chronith.Application.DTOs;

public sealed record AuditEntryDto(
    Guid Id,
    string UserId,
    string UserRole,
    string EntityType,
    Guid EntityId,
    string Action,
    string? OldValues,
    string? NewValues,
    string? Metadata,
    DateTimeOffset Timestamp);
