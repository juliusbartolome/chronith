namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class TenantNotificationConfigEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ChannelType { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Settings { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
