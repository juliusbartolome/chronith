using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.Recurring;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record ListRecurrenceRulesQuery
    : IRequest<IReadOnlyList<RecurrenceRuleDto>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ListRecurrenceRulesHandler(
    IRecurrenceRuleRepository recurrenceRuleRepo)
    : IRequestHandler<ListRecurrenceRulesQuery, IReadOnlyList<RecurrenceRuleDto>>
{
    public async Task<IReadOnlyList<RecurrenceRuleDto>> Handle(
        ListRecurrenceRulesQuery query, CancellationToken ct)
    {
        var rules = await recurrenceRuleRepo.GetAllAsync(ct);
        return rules.Select(r => r.ToDto()).ToList();
    }
}
