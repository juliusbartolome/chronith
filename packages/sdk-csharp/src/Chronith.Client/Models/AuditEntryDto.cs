namespace Chronith.Client.Models;

/// <summary>
/// Represents a single audit log entry returned by the Chronith API.
/// </summary>
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
