using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Staff;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record DeleteStaffCommand : IRequest
{
    public required Guid StaffId { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class DeleteStaffValidator : AbstractValidator<DeleteStaffCommand>
{
    public DeleteStaffValidator()
    {
        RuleFor(x => x.StaffId).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class DeleteStaffHandler(
    ITenantContext tenantContext,
    IStaffMemberRepository staffRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteStaffCommand>
{
    public async Task Handle(DeleteStaffCommand cmd, CancellationToken ct)
    {
        var staff = await staffRepo.GetByIdAsync(tenantContext.TenantId, cmd.StaffId, ct)
            ?? throw new NotFoundException("StaffMember", cmd.StaffId);

        staff.SoftDelete();
        await staffRepo.UpdateAsync(staff, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
