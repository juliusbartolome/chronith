using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.TimeBlocks;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record ListTimeBlocksQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? BookingTypeId = null,
    Guid? StaffMemberId = null)
    : IRequest<IReadOnlyList<TimeBlockDto>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ListTimeBlocksHandler(
    ITenantContext tenantContext,
    ITimeBlockRepository timeBlockRepo)
    : IRequestHandler<ListTimeBlocksQuery, IReadOnlyList<TimeBlockDto>>
{
    public async Task<IReadOnlyList<TimeBlockDto>> Handle(
        ListTimeBlocksQuery query, CancellationToken ct)
    {
        var blocks = await timeBlockRepo.ListInRangeAsync(
            tenantContext.TenantId,
            query.BookingTypeId,
            query.StaffMemberId,
            query.From,
            query.To,
            ct);

        return blocks.Select(b => b.ToDto()).ToList();
    }
}
