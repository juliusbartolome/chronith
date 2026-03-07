namespace Chronith.Infrastructure.Notifications;

public sealed class FirebasePushOptions
{
    public string ServiceAccountJson { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
}
