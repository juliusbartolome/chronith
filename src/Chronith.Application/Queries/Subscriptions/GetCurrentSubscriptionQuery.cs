using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Subscriptions;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetCurrentSubscriptionQuery : IRequest<TenantSubscriptionDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetCurrentSubscriptionQueryHandler(
    ITenantSubscriptionRepository subRepo,
    ITenantPlanRepository planRepo,
    ITenantContext tenantContext
) : IRequestHandler<GetCurrentSubscriptionQuery, TenantSubscriptionDto>
{
    public async Task<TenantSubscriptionDto> Handle(
        GetCurrentSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var sub = await subRepo.GetActiveByTenantIdAsync(
            tenantContext.TenantId, cancellationToken)
            ?? throw new NotFoundException("TenantSubscription", tenantContext.TenantId);

        var plan = await planRepo.GetByIdAsync(sub.PlanId, cancellationToken)
            ?? throw new NotFoundException("TenantPlan", sub.PlanId);

        return sub.ToDto(plan.Name);
    }
}
