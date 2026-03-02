using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Payments.PayMongo;

public sealed class PayMongoProvider(
    IOptions<PayMongoOptions> options,
    HttpClient httpClient)
    : IPaymentProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderName => "PayMongo";

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        Booking booking, string currency, CancellationToken ct)
    {
        // PayMongo uses amount in centavos (multiply by 100)
        var requestBody = new
        {
            data = new
            {
                attributes = new
                {
                    amount = 10000, // TODO: booking should carry price in future
                    currency,
                    description = $"Booking {booking.Id}",
                    remarks = booking.Id.ToString()
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Value.SecretKey}:"));

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/links")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseBody);
        var data = doc.RootElement.GetProperty("data").GetProperty("attributes");
        var checkoutUrl = data.GetProperty("checkout_url").GetString()!;
        var referenceNumber = data.GetProperty("reference_number").GetString()!;

        return new PaymentIntentResult(referenceNumber, checkoutUrl);
    }

    public bool ValidateWebhookSignature(string rawBody, string signatureHeader)
    {
        // PayMongo signature format: t=<timestamp>,te=<hmac-sha256-hex>
        // Signed payload: "<timestamp>.<rawBody>"
        try
        {
            var parts = signatureHeader.Split(',');
            var timestamp = parts.First(p => p.StartsWith("t=")).Substring(2);
            var signature = parts.First(p => p.StartsWith("te=")).Substring(3);

            var toSign = $"{timestamp}.{rawBody}";
            var key = Encoding.UTF8.GetBytes(options.Value.WebhookSecret);
            var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(toSign));
            var expectedHex = Convert.ToHexStringLower(expected);

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedHex),
                Encoding.UTF8.GetBytes(signature));
        }
        catch
        {
            return false;
        }
    }

    public PaymentEvent ParsePaymentEvent(string rawBody)
    {
        using var doc = JsonDocument.Parse(rawBody);
        var attributes = doc.RootElement
            .GetProperty("data")
            .GetProperty("attributes");

        var status = attributes.GetProperty("status").GetString();
        var referenceNumber = attributes.TryGetProperty("reference_number", out var refProp)
            ? refProp.GetString() ?? string.Empty
            : string.Empty;

        return new PaymentEvent(ExternalId: referenceNumber, IsPaid: status == "paid");
    }
}
