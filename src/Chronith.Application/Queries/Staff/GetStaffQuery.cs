using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Staff;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetStaffQuery(Guid StaffId) : IRequest<StaffMemberDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetStaffHandler(
    ITenantContext tenantContext,
    IStaffMemberRepository staffRepo)
    : IRequestHandler<GetStaffQuery, StaffMemberDto>
{
    public async Task<StaffMemberDto> Handle(GetStaffQuery query, CancellationToken ct)
    {
        var staff = await staffRepo.GetByIdAsync(tenantContext.TenantId, query.StaffId, ct)
            ?? throw new NotFoundException("StaffMember", query.StaffId);

        return staff.ToDto();
    }
}
