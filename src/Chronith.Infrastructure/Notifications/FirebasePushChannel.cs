using Chronith.Application.Interfaces;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Notifications;

public sealed class FirebasePushChannel(
    IOptions<FirebasePushOptions> options,
    ILogger<FirebasePushChannel> logger) : INotificationChannel
{
    private static bool _initialized;
    private static readonly object Lock = new();

    public string ChannelType => "push";

    public async Task SendAsync(NotificationMessage message, CancellationToken ct)
    {
        EnsureInitialized();

        var fcmMessage = new Message
        {
            Token = message.Recipient,
            Notification = new Notification
            {
                Title = message.Subject,
                Body = message.Body
            }
        };

        var response = await FirebaseMessaging.DefaultInstance.SendAsync(fcmMessage, ct);
        logger.LogInformation("Push notification sent, message ID: {MessageId}", response);
    }

    private void EnsureInitialized() => InitializeApp(options.Value);

    private static void InitializeApp(FirebasePushOptions opts)
    {
        if (_initialized) return;

        lock (Lock)
        {
            if (_initialized) return;

            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromJson(opts.ServiceAccountJson),
                ProjectId = opts.ProjectId
            });
            _initialized = true;
        }
    }
}
