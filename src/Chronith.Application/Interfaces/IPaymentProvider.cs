using Chronith.Application.DTOs;

namespace Chronith.Application.Interfaces;

public interface IPaymentProvider
{
    string ProviderName { get; }

    Task<CreateCheckoutResult> CreateCheckoutSessionAsync(
        CreateCheckoutRequest request,
        CancellationToken ct);

    bool ValidateWebhook(WebhookValidationContext context);

    WebhookPaymentEvent ParseWebhookPayload(string rawBody);
}
