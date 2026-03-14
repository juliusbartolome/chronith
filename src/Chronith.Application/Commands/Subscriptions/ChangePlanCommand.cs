using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Subscriptions;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record ChangePlanCommand : IRequest<TenantSubscriptionDto>
{
    public required Guid NewPlanId { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class ChangePlanCommandValidator : AbstractValidator<ChangePlanCommand>
{
    public ChangePlanCommandValidator()
    {
        RuleFor(x => x.NewPlanId).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ChangePlanCommandHandler(
    ITenantSubscriptionRepository subRepo,
    ITenantPlanRepository planRepo,
    ISubscriptionProvider subscriptionProvider,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork
) : IRequestHandler<ChangePlanCommand, TenantSubscriptionDto>
{
    public async Task<TenantSubscriptionDto> Handle(
        ChangePlanCommand command, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;

        var sub = await subRepo.GetActiveByTenantIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("TenantSubscription", tenantId);

        var newPlan = await planRepo.GetByIdAsync(command.NewPlanId, cancellationToken)
            ?? throw new NotFoundException("TenantPlan", command.NewPlanId);

        // Update with payment provider if there's an existing provider subscription
        if (sub.PaymentProviderSubscriptionId is not null)
        {
            await subscriptionProvider.UpdateSubscriptionAsync(
                sub.PaymentProviderSubscriptionId,
                new UpdateSubscriptionRequest(command.NewPlanId),
                cancellationToken);
        }

        // Cancel old subscription, create new one on the new plan
        sub.Cancel("Plan changed");
        await subRepo.UpdateAsync(sub, cancellationToken);

        var newSub = newPlan.PriceCentavos == 0
            ? TenantSubscription.CreateTrial(tenantId, newPlan.Id)
            : TenantSubscription.CreatePaid(
                tenantId,
                newPlan.Id,
                sub.PaymentProviderSubscriptionId ?? Guid.NewGuid().ToString(),
                sub.CurrentPeriodStart,
                sub.CurrentPeriodEnd);

        await subRepo.AddAsync(newSub, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return newSub.ToDto(newPlan.Name);
    }
}
