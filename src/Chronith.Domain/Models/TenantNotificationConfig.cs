namespace Chronith.Domain.Models;

public sealed class TenantNotificationConfig
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ChannelType { get; private set; } = string.Empty; // "email", "sms", "push"
    public bool IsEnabled { get; private set; }
    public string Settings { get; private set; } = "{}"; // JSONB
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal TenantNotificationConfig() { }

    public static TenantNotificationConfig Create(Guid tenantId, string channelType, string settings)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ChannelType = channelType,
            IsEnabled = true,
            Settings = settings,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    public void UpdateSettings(string settings)
    {
        Settings = settings;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Enable()
    {
        IsEnabled = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
