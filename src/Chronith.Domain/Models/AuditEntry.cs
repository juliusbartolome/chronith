namespace Chronith.Domain.Models;

public sealed class AuditEntry
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string UserRole { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public string? Metadata { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }

    internal AuditEntry() { }

    public static AuditEntry Create(
        Guid tenantId,
        string userId,
        string userRole,
        string entityType,
        Guid entityId,
        string action,
        string? oldValues,
        string? newValues,
        string? metadata)
    {
        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            UserRole = userRole,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValues = oldValues,
            NewValues = newValues,
            Metadata = metadata,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
