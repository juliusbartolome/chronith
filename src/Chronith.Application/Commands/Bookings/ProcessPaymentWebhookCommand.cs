using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Chronith.Application.Commands.Bookings;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record ProcessPaymentWebhookCommand : IRequest
{
    public required Guid TenantId { get; init; }
    public required string ProviderName { get; init; }
    public required string RawBody { get; init; }
    public required IDictionary<string, string> Headers { get; init; }
    public string? SourceIpAddress { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class ProcessPaymentWebhookValidator
    : AbstractValidator<ProcessPaymentWebhookCommand>
{
    public ProcessPaymentWebhookValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ProviderName).NotEmpty();
        RuleFor(x => x.RawBody).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ProcessPaymentWebhookHandler(
    ITenantPaymentProviderResolver resolver,
    IBookingRepository bookingRepo,
    IBookingTypeRepository bookingTypeRepo,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    ILogger<ProcessPaymentWebhookHandler> logger)
    : IRequestHandler<ProcessPaymentWebhookCommand>
{
    public async Task Handle(ProcessPaymentWebhookCommand cmd, CancellationToken ct)
    {
        // Resolve the provider per-tenant — loads webhook secret from the tenant's payment config
        var provider = await resolver.ResolveAsync(cmd.TenantId, cmd.ProviderName, ct);
        if (provider is null)
        {
            logger.LogWarning("Failed to resolve payment provider {ProviderName} for tenant {TenantId}", cmd.ProviderName, cmd.TenantId);
            throw new UnauthorizedException("Webhook validation failed");
        }

        logger.LogInformation("Payment provider resolved: {ProviderName} for tenant {TenantId}", cmd.ProviderName, cmd.TenantId);

        // Validate webhook authenticity using the tenant's webhook secret
        var validationContext = new WebhookValidationContext(
            cmd.Headers, cmd.RawBody, cmd.SourceIpAddress);

        if (!provider.ValidateWebhook(validationContext))
        {
            logger.LogWarning("Webhook signature validation failed for {TenantId}/{ProviderName}", cmd.TenantId, cmd.ProviderName);
            throw new UnauthorizedException("Webhook validation failed");
        }

        // Parse the provider-specific payload
        var paymentEvent = provider.ParseWebhookPayload(cmd.RawBody);

        logger.LogInformation("Parsed payment event: {EventType}, transaction {ProviderTransactionId}", paymentEvent.EventType, paymentEvent.ProviderTransactionId);

        // Find booking by provider transaction ID (scoped to the tenant from the route)
        var booking = await bookingRepo.GetByPaymentReferenceAsync(
                cmd.TenantId, paymentEvent.ProviderTransactionId, ct);

        if (booking is null)
        {
            logger.LogWarning("Booking not found for payment reference {ProviderTransactionId} on tenant {TenantId}", paymentEvent.ProviderTransactionId, cmd.TenantId);
            throw new NotFoundException("Booking",
                $"PaymentReference={paymentEvent.ProviderTransactionId}");
        }

        // Look up the booking type for the notification slug
        var bookingType = await bookingTypeRepo.GetByIdAsync(booking.BookingTypeId, ct);

        var from = booking.Status;

        if (paymentEvent.EventType == PaymentEventType.Success)
            booking.ConfirmPayment("payment-webhook", cmd.ProviderName);
        else
        {
            logger.LogWarning("Non-success payment event {EventType} for transaction {ProviderTransactionId} — transitioning to PaymentFailed", paymentEvent.EventType, paymentEvent.ProviderTransactionId);
            booking.FailPayment("payment-webhook", cmd.ProviderName);
        }

        logger.LogInformation("Booking {BookingId} transitioned from {FromStatus} to {ToStatus}", booking.Id, from, booking.Status);

        await bookingRepo.UpdateAsync(booking, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await publisher.Publish(
            new Notifications.BookingStatusChangedNotification(
                BookingId: booking.Id,
                TenantId: booking.TenantId,
                BookingTypeId: booking.BookingTypeId,
                BookingTypeSlug: bookingType?.Slug ?? "unknown",
                FromStatus: from,
                ToStatus: booking.Status,
                Start: booking.Start,
                End: booking.End,
                CustomerId: booking.CustomerId,
                CustomerEmail: booking.CustomerEmail,
                CustomerFirstName: booking.FirstName,
                CustomerLastName: booking.LastName,
                CustomerMobile: booking.Mobile),
            ct);
    }
}
