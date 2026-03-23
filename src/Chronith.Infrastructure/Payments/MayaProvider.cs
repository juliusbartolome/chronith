using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Payments;

public sealed class MayaProvider(
    IOptions<MayaOptions> options,
    IHttpClientFactory httpClientFactory)
    : IPaymentProvider
{
    public string ProviderName => "Maya";

    // ── New API ───────────────────────────────────────────────────────────────

    public async Task<CreateCheckoutResult> CreateCheckoutSessionAsync(
        CreateCheckoutRequest request, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("Maya");
        var authHeader = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.Value.PublicApiKey}:"));

        var amountDecimal = request.AmountInCentavos / 100.0m;

        var payload = new
        {
            totalAmount = new
            {
                value = amountDecimal,
                currency = request.Currency
            },
            buyer = new { },
            items = new[]
            {
                new
                {
                    name = request.Description,
                    totalAmount = new { value = amountDecimal, currency = request.Currency },
                    quantity = 1
                }
            },
            redirectUrl = new
            {
                success = request.SuccessUrl
                    ?? options.Value.SuccessUrl.Replace("{bookingId}", request.BookingId.ToString()),
                failure = request.CancelUrl
                    ?? options.Value.FailureUrl.Replace("{bookingId}", request.BookingId.ToString()),
                cancel = request.CancelUrl
                    ?? options.Value.FailureUrl.Replace("{bookingId}", request.BookingId.ToString())
            },
            requestReferenceNumber = request.BookingId.ToString()
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            $"{options.Value.BaseUrl}/checkout/v1/checkouts");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", authHeader);
        httpRequest.Content = JsonContent.Create(payload);

        var response = await client.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var checkoutUrl = json.GetProperty("redirectUrl").GetString()!;
        var checkoutId = json.GetProperty("checkoutId").GetString()!;

        return new CreateCheckoutResult(checkoutUrl, checkoutId);
    }

    public bool ValidateWebhook(WebhookValidationContext context)
    {
        // Maya uses IP whitelisting only — no HMAC signature
        if (string.IsNullOrEmpty(context.SourceIpAddress))
            return false;

        return options.Value.AllowedWebhookIps.Contains(context.SourceIpAddress);
    }

    public WebhookPaymentEvent ParseWebhookPayload(string rawBody)
    {
        var json = JsonDocument.Parse(rawBody).RootElement;
        var id = json.GetProperty("id").GetString()!;
        var status = json.TryGetProperty("paymentStatus", out var statusProp)
            ? statusProp.GetString()
            : null;

        var eventType = status switch
        {
            "PAYMENT_SUCCESS" => PaymentEventType.Success,
            "PAYMENT_FAILED" => PaymentEventType.Failed,
            "PAYMENT_EXPIRED" => PaymentEventType.Expired,
            "PAYMENT_CANCELLED" => PaymentEventType.Cancelled,
            _ => PaymentEventType.Failed
        };

        return new WebhookPaymentEvent(id, eventType);
    }

    // ── Legacy API (kept until CreateBookingCommand migration in Task 12) ────

    public Task<PaymentIntentResult> CreatePaymentIntentAsync(
        Booking booking, string currency, CancellationToken ct)
    {
        // Maya legacy: not used in production, placeholder for interface compliance
        return Task.FromResult(new PaymentIntentResult(
            ExternalId: $"maya-{booking.Id}",
            CheckoutUrl: $"{options.Value.BaseUrl}/checkout/legacy/{booking.Id}"));
    }

    public bool ValidateWebhookSignature(string rawBody, string signatureHeader)
    {
        // Maya doesn't use HMAC signatures — IP whitelisting only
        return false;
    }

    public PaymentEvent ParsePaymentEvent(string rawBody)
    {
        var json = JsonDocument.Parse(rawBody).RootElement;
        var id = json.GetProperty("id").GetString()!;
        var status = json.TryGetProperty("paymentStatus", out var statusProp)
            ? statusProp.GetString()
            : null;

        return new PaymentEvent(ExternalId: id, IsPaid: status == "PAYMENT_SUCCESS");
    }
}
