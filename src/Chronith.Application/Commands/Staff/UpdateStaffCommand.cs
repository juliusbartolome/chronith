using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Staff;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record UpdateStaffCommand : IRequest<StaffMemberDto>, IAuditable
{
    public required Guid StaffId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public IReadOnlyList<StaffAvailabilityWindowInput> AvailabilityWindows { get; init; } = [];

    public Guid EntityId => StaffId;
    public string EntityType => "StaffMember";
    public string Action => "Update";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class UpdateStaffValidator : AbstractValidator<UpdateStaffCommand>
{
    public UpdateStaffValidator()
    {
        RuleFor(x => x.StaffId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class UpdateStaffHandler(
    ITenantContext tenantContext,
    IStaffMemberRepository staffRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateStaffCommand, StaffMemberDto>
{
    public async Task<StaffMemberDto> Handle(UpdateStaffCommand cmd, CancellationToken ct)
    {
        var staff = await staffRepo.GetByIdAsync(tenantContext.TenantId, cmd.StaffId, ct)
            ?? throw new NotFoundException("StaffMember", cmd.StaffId);

        var windows = cmd.AvailabilityWindows
            .Select(w => new StaffAvailabilityWindow(w.DayOfWeek, w.StartTime, w.EndTime))
            .ToList();

        staff.Update(cmd.Name, cmd.Email, windows);
        await staffRepo.UpdateAsync(staff, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return staff.ToDto();
    }
}
