using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Subscriptions;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record SubscribeCommand : IRequest<TenantSubscriptionDto>
{
    public required Guid PlanId { get; init; }
    public string? PaymentMethodToken { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class SubscribeCommandValidator : AbstractValidator<SubscribeCommand>
{
    public SubscribeCommandValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class SubscribeCommandHandler(
    ITenantSubscriptionRepository subRepo,
    ITenantPlanRepository planRepo,
    ISubscriptionProvider subscriptionProvider,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork
) : IRequestHandler<SubscribeCommand, TenantSubscriptionDto>
{
    public async Task<TenantSubscriptionDto> Handle(
        SubscribeCommand command, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;

        var existing = await subRepo.GetActiveByTenantIdAsync(tenantId, cancellationToken);
        if (existing is not null)
            throw new ConflictException(
                $"Tenant {tenantId} already has an active subscription. Use ChangePlan to switch plans.");

        var plan = await planRepo.GetByIdAsync(command.PlanId, cancellationToken)
            ?? throw new NotFoundException("TenantPlan", command.PlanId);

        TenantSubscription sub;

        if (plan.PriceCentavos == 0)
        {
            // Free plan — create trial immediately, no payment provider needed
            sub = TenantSubscription.CreateTrial(tenantId, plan.Id);
        }
        else
        {
            var result = await subscriptionProvider.CreateSubscriptionAsync(
                new CreateSubscriptionRequest(
                    tenantId,
                    plan.Id,
                    string.Empty, // ITenantContext has no TenantEmail
                    command.PaymentMethodToken),
                cancellationToken);

            sub = TenantSubscription.CreatePaid(
                tenantId,
                plan.Id,
                result.ProviderSubscriptionId,
                result.PeriodStart,
                result.PeriodEnd);
        }

        await subRepo.AddAsync(sub, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return sub.ToDto(plan.Name);
    }
}
