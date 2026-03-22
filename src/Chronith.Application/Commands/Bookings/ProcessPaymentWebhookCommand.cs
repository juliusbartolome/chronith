using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

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
    IPublisher publisher)
    : IRequestHandler<ProcessPaymentWebhookCommand>
{
    public async Task Handle(ProcessPaymentWebhookCommand cmd, CancellationToken ct)
    {
        // Resolve the provider per-tenant — loads webhook secret from the tenant's payment config
        var provider = await resolver.ResolveAsync(cmd.TenantId, cmd.ProviderName, ct);
        if (provider is null)
            throw new UnauthorizedException("Webhook validation failed");

        // Validate webhook authenticity using the tenant's webhook secret
        var validationContext = new WebhookValidationContext(
            cmd.Headers, cmd.RawBody, cmd.SourceIpAddress);

        if (!provider.ValidateWebhook(validationContext))
            throw new UnauthorizedException("Webhook validation failed");

        // Parse the provider-specific payload
        var paymentEvent = provider.ParseWebhookPayload(cmd.RawBody);

        // Only process success events — others are acknowledged but not acted on
        if (paymentEvent.EventType != PaymentEventType.Success)
            return;

        // Find booking by provider transaction ID (scoped to the tenant from the route)
        var booking = await bookingRepo.GetByPaymentReferenceAsync(
                cmd.TenantId, paymentEvent.ProviderTransactionId, ct)
            ?? throw new NotFoundException("Booking",
                $"PaymentReference={paymentEvent.ProviderTransactionId}");

        // Look up the booking type for the notification slug
        var bookingType = await bookingTypeRepo.GetByIdAsync(booking.BookingTypeId, ct);

        var from = booking.Status;
        booking.Pay("payment-webhook", cmd.ProviderName);

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
                CustomerEmail: booking.CustomerEmail),
            ct);
    }
}
