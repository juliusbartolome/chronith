using Chronith.Application.Interfaces;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Chronith.Infrastructure.Notifications;

public sealed class SmtpEmailChannel(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailChannel> logger) : INotificationChannel
{
    public string ChannelType => "email";

    public async Task SendAsync(NotificationMessage message, CancellationToken ct)
    {
        var opts = options.Value;

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(opts.FromName, opts.FromAddress));
        email.To.Add(MailboxAddress.Parse(message.Recipient));
        email.Subject = message.Subject;
        email.Body = new TextPart("html") { Text = message.Body };

        using var client = new SmtpClient();
        await client.ConnectAsync(opts.Host, opts.Port, opts.UseSsl, ct);

        if (!string.IsNullOrEmpty(opts.Username))
            await client.AuthenticateAsync(opts.Username, opts.Password, ct);

        await client.SendAsync(email, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("Email sent to {Recipient}", message.Recipient);
    }
}
