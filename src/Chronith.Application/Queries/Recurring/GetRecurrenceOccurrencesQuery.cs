using Chronith.Application.Behaviors;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Recurring;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetRecurrenceOccurrencesQuery(Guid Id, DateOnly From, DateOnly To)
    : IRequest<IReadOnlyList<DateOnly>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetRecurrenceOccurrencesHandler(
    IRecurrenceRuleRepository recurrenceRuleRepo)
    : IRequestHandler<GetRecurrenceOccurrencesQuery, IReadOnlyList<DateOnly>>
{
    public async Task<IReadOnlyList<DateOnly>> Handle(
        GetRecurrenceOccurrencesQuery query, CancellationToken ct)
    {
        var rule = await recurrenceRuleRepo.GetByIdAsync(query.Id, ct)
            ?? throw new NotFoundException("RecurrenceRule", query.Id);

        return rule.ComputeOccurrences(query.From, query.To);
    }
}
