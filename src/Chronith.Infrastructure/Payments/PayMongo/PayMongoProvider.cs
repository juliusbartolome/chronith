using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Payments.PayMongo;

public sealed class PayMongoProvider(
    IOptions<PayMongoOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<PayMongoProvider> logger)
    : IPaymentProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderName => "PayMongo";

    // ── New API ───────────────────────────────────────────────────────────────

    public async Task<CreateCheckoutResult> CreateCheckoutSessionAsync(
        CreateCheckoutRequest request, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("PayMongo");
        var authHeader = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.Value.SecretKey}:"));

        var payload = new
        {
            data = new
            {
                attributes = new
                {
                    line_items = new[]
                    {
                        new
                        {
                            name = request.Description,
                            amount = request.AmountInCentavos,
                            currency = request.Currency,
                            quantity = 1
                        }
                    },
                    payment_method_types = new[] { "gcash", "card", "grab_pay", "paymaya" },
                    success_url = request.SuccessUrl
                        ?? options.Value.SuccessUrl.Replace("{bookingId}", request.BookingId.ToString()),
                    cancel_url = request.CancelUrl
                        ?? options.Value.FailureUrl.Replace("{bookingId}", request.BookingId.ToString()),
                    description = request.Description,
                    reference_number = request.BookingId.ToString()
                }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            $"{options.Value.BaseUrl}/checkout_sessions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", authHeader);
        httpRequest.Content = JsonContent.Create(payload);

        var response = await client.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("PayMongo API error: {StatusCode} {ErrorBody}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var checkoutUrl = json.GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("checkout_url")
            .GetString()!;
        var checkoutId = json.GetProperty("data")
            .GetProperty("id")
            .GetString()!;

        logger.LogInformation("PayMongo checkout created: {CheckoutId} for booking {BookingId}",
            checkoutId, request.BookingId);

        return new CreateCheckoutResult(checkoutUrl, checkoutId);
    }

    public bool ValidateWebhook(WebhookValidationContext context)
    {
        // Header key is lowercase when collected from ASP.NET Core request headers
        if (!context.Headers.TryGetValue("paymongo-signature", out var signature))
        {
            logger.LogWarning("PayMongo webhook missing paymongo-signature header");
            return false;
        }

        return ValidateWebhookSignature(context.RawBody, signature);
    }

    public WebhookPaymentEvent ParseWebhookPayload(string rawBody)
    {
        var json = JsonDocument.Parse(rawBody).RootElement;
        var eventType = json.GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("type")
            .GetString()!;
        var resourceId = json.GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("data")
            .GetProperty("id")
            .GetString()!;

        var paymentEventType = eventType switch
        {
            "checkout_session.payment.paid" => PaymentEventType.Success,
            "payment.paid" => PaymentEventType.Success,
            "payment.failed" => PaymentEventType.Failed,
            _ => PaymentEventType.Failed
        };

        if (paymentEventType == PaymentEventType.Failed
            && eventType is not "payment.failed")
        {
            logger.LogDebug("Unknown PayMongo event type {EventType} mapped to Failed", eventType);
        }

        return new WebhookPaymentEvent(resourceId, paymentEventType);
    }

    // ── Legacy API (kept until CreateBookingCommand migration in Task 12) ────

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        Booking booking, string currency, CancellationToken ct)
    {
        var httpClient = httpClientFactory.CreateClient("PayMongo");

        var requestBody = new
        {
            data = new
            {
                attributes = new
                {
                    amount = 10000,
                    currency,
                    description = $"Booking {booking.Id}",
                    remarks = booking.Id.ToString()
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Value.SecretKey}:"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/links")
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
        try
        {
            var parts = signatureHeader.Split(',');
            var timestamp = parts.First(p => p.StartsWith("t=")).Substring(2);

            // Accept either te= (test) or li= (live) signature
            var signature = parts.FirstOrDefault(p => p.StartsWith("te="))?.Substring(3)
                ?? parts.FirstOrDefault(p => p.StartsWith("li="))?.Substring(3);

            if (signature is null)
                return false;

            // Replay protection
            if (!long.TryParse(timestamp, out var unixTimestamp))
                return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var tolerance = options.Value.WebhookTimestampToleranceSeconds;
            if (Math.Abs(now - unixTimestamp) > tolerance)
            {
                logger.LogWarning("PayMongo webhook timestamp outside tolerance: {Timestamp} (now: {Now}, tolerance: {Tolerance}s)",
                    unixTimestamp, now, tolerance);
                return false;
            }

            var toSign = $"{timestamp}.{rawBody}";
            var key = Encoding.UTF8.GetBytes(options.Value.WebhookSecret);
            var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(toSign));
            var expectedHex = Convert.ToHexStringLower(expected);

            var isValid = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedHex),
                Encoding.UTF8.GetBytes(signature));

            if (!isValid)
                logger.LogWarning("PayMongo webhook signature mismatch");

            return isValid;
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {
            logger.LogWarning(ex, "PayMongo webhook signature validation failed");
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
