using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.Public;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record PublicListStaffQuery(Guid TenantId)
    : IRequest<IReadOnlyList<StaffMemberDto>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class PublicListStaffHandler(IStaffMemberRepository staffRepo)
    : IRequestHandler<PublicListStaffQuery, IReadOnlyList<StaffMemberDto>>
{
    public async Task<IReadOnlyList<StaffMemberDto>> Handle(
        PublicListStaffQuery query, CancellationToken ct)
    {
        var staff = await staffRepo.ListAsync(query.TenantId, ct);
        return staff.Select(s => s.ToDto()).ToList();
    }
}
