namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class AuditEntryEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? Metadata { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
