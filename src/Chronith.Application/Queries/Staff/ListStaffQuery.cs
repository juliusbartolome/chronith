using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.Staff;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record ListStaffQuery : IRequest<IReadOnlyList<StaffMemberDto>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ListStaffHandler(
    ITenantContext tenantContext,
    IStaffMemberRepository staffRepo)
    : IRequestHandler<ListStaffQuery, IReadOnlyList<StaffMemberDto>>
{
    public async Task<IReadOnlyList<StaffMemberDto>> Handle(
        ListStaffQuery query, CancellationToken ct)
    {
        var staff = await staffRepo.ListAsync(tenantContext.TenantId, ct);
        return staff.Select(s => s.ToDto()).ToList();
    }
}
