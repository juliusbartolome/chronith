using Chronith.Application.Commands.Bookings;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Payments;

public sealed class PaymentWebhookEndpoint(ISender sender)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/webhooks/payments/{provider}");
        AllowAnonymous(); // Webhooks come from external providers, no JWT
        Options(x => x.WithTags("Payments").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var provider = Route<string>("provider")!;

        // Read raw body for signature verification
        HttpContext.Request.EnableBuffering();
        using var reader = new StreamReader(HttpContext.Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        // Collect headers (lowercased keys for consistent matching)
        var headers = HttpContext.Request.Headers
            .ToDictionary(h => h.Key.ToLowerInvariant(), h => h.Value.ToString());

        var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        await sender.Send(new ProcessPaymentWebhookCommand
        {
            ProviderName = provider,
            RawBody = rawBody,
            Headers = headers,
            SourceIpAddress = sourceIp
        }, ct);

        await Send.NoContentAsync(ct);
    }
}
