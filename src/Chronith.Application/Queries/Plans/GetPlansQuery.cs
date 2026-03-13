using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.Plans;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetPlansQuery : IRequest<IReadOnlyList<TenantPlanDto>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetPlansQueryHandler(ITenantPlanRepository planRepo)
    : IRequestHandler<GetPlansQuery, IReadOnlyList<TenantPlanDto>>
{
    public async Task<IReadOnlyList<TenantPlanDto>> Handle(
        GetPlansQuery request, CancellationToken cancellationToken)
    {
        var plans = await planRepo.GetActivePlansAsync(cancellationToken);
        return plans.Select(p => p.ToDto()).ToList().AsReadOnly();
    }
}
