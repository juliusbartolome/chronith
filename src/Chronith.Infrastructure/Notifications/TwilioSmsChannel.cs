using System.Net.Http.Headers;
using System.Text;
using Chronith.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Notifications;

public sealed class TwilioSmsChannel(
    IHttpClientFactory httpClientFactory,
    IOptions<TwilioOptions> options,
    ILogger<TwilioSmsChannel> logger) : INotificationChannel
{
    public string ChannelType => "sms";

    public async Task SendAsync(NotificationMessage message, CancellationToken ct)
    {
        var opts = options.Value;
        var client = httpClientFactory.CreateClient("Twilio");

        var url = $"https://api.twilio.com/2010-04-01/Accounts/{opts.AccountSid}/Messages.json";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["From"] = opts.FromNumber,
            ["To"] = message.Recipient,
            ["Body"] = message.Body
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{opts.AccountSid}:{opts.AuthToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        logger.LogInformation("SMS notification sent successfully for subject '{Subject}'", message.Subject);
    }
}
