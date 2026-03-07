using Chronith.Application.Commands.Bookings;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Payments;

public sealed class PaymentWebhookRequest
{
    public Guid BookingId { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
}

public sealed class PaymentWebhookEndpoint(ISender sender)
    : Endpoint<PaymentWebhookRequest, BookingDto>
{
    public override void Configure()
    {
        Post("/webhooks/payment");
        Roles("TenantPaymentService");
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(PaymentWebhookRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new PayBookingCommand
        {
            BookingId = req.BookingId,
            PaymentReference = req.PaymentReference
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
