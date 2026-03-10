namespace Chronith.Domain.Models;

public sealed class NotificationTemplate
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string ChannelType { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal NotificationTemplate() { }

    public static NotificationTemplate Create(
        Guid tenantId,
        string eventType,
        string channelType,
        string? subject,
        string body)
    {
        return new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = eventType,
            ChannelType = channelType,
            Subject = subject,
            Body = body,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateBody(string? subject, string body)
    {
        Subject = subject;
        Body = body;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
