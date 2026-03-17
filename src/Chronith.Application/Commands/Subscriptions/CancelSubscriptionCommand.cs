using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Commands.Subscriptions;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CancelSubscriptionCommand : IRequest<Unit>
{
    public string? Reason { get; init; }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CancelSubscriptionCommandHandler(
    ITenantSubscriptionRepository subRepo,
    ISubscriptionProvider subscriptionProvider,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork
) : IRequestHandler<CancelSubscriptionCommand, Unit>
{
    public async Task<Unit> Handle(
        CancelSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;

        var sub = await subRepo.GetActiveByTenantIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("TenantSubscription", tenantId);

        if (sub.PaymentProviderSubscriptionId is not null)
        {
            await subscriptionProvider.CancelSubscriptionAsync(
                sub.PaymentProviderSubscriptionId,
                cancellationToken);
        }

        sub.Cancel(command.Reason);
        await subRepo.UpdateAsync(sub, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
