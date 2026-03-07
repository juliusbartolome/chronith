namespace Chronith.Application.Interfaces;

public interface INotificationChannel
{
    string ChannelType { get; }
    Task SendAsync(NotificationMessage message, CancellationToken ct);
}

public record NotificationMessage(
    string Recipient,
    string Subject,
    string Body,
    string? TemplateId = null,
    IDictionary<string, string>? Metadata = null);
