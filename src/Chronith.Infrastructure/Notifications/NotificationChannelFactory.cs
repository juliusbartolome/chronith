using Chronith.Application.Interfaces;

namespace Chronith.Infrastructure.Notifications;

public sealed class NotificationChannelFactory(IEnumerable<INotificationChannel> channels)
{
    private readonly Dictionary<string, INotificationChannel> _channels =
        channels.ToDictionary(c => c.ChannelType, StringComparer.OrdinalIgnoreCase);

    public INotificationChannel? GetChannel(string channelType)
        => _channels.GetValueOrDefault(channelType);

    public IReadOnlyList<string> SupportedChannels => [.. _channels.Keys];
}
