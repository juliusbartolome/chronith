using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Recurring;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetRecurrenceRuleQuery(Guid Id)
    : IRequest<RecurrenceRuleDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetRecurrenceRuleHandler(
    IRecurrenceRuleRepository recurrenceRuleRepo)
    : IRequestHandler<GetRecurrenceRuleQuery, RecurrenceRuleDto>
{
    public async Task<RecurrenceRuleDto> Handle(
        GetRecurrenceRuleQuery query, CancellationToken ct)
    {
        var rule = await recurrenceRuleRepo.GetByIdAsync(query.Id, ct)
            ?? throw new NotFoundException("RecurrenceRule", query.Id);

        return rule.ToDto();
    }
}
