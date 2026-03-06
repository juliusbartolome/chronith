using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Webhooks.RetryWebhookDelivery;

// ── Command ───────────────────────────────────────────────────────────────────

public sealed record RetryWebhookDeliveryCommand(
    Guid WebhookId,
    Guid DeliveryId) : IRequest;

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class RetryWebhookDeliveryCommandValidator : AbstractValidator<RetryWebhookDeliveryCommand>
{
    public RetryWebhookDeliveryCommandValidator()
    {
        RuleFor(x => x.WebhookId).NotEmpty();
        RuleFor(x => x.DeliveryId).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class RetryWebhookDeliveryCommandHandler(
    ITenantContext tenantContext,
    IWebhookRepository webhookRepository,
    IWebhookOutboxRepository outboxRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RetryWebhookDeliveryCommand>
{
    public async Task Handle(RetryWebhookDeliveryCommand request, CancellationToken cancellationToken)
    {
        // Verify webhook belongs to this tenant
        _ = await webhookRepository.GetByIdAsync(tenantContext.TenantId, request.WebhookId, cancellationToken)
            ?? throw new NotFoundException("Webhook", request.WebhookId);

        var (foundWebhookId, canRetry) = await outboxRepository.ResetForRetryAsync(
            request.DeliveryId, cancellationToken);

        if (foundWebhookId is null)
            throw new NotFoundException("WebhookOutboxEntry", request.DeliveryId);

        if (foundWebhookId != request.WebhookId)
            throw new NotFoundException("WebhookOutboxEntry", request.DeliveryId);

        if (!canRetry)
            throw new InvalidStateTransitionException(
                "Cannot retry a delivery that is not in Failed status. Only Failed entries can be retried.");

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
