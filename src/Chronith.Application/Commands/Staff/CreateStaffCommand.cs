using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Staff;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CreateStaffCommand : IRequest<StaffMemberDto>, IAuditable
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public Guid? TenantUserId { get; init; }
    public IReadOnlyList<StaffAvailabilityWindowInput> AvailabilityWindows { get; init; } = [];

    // IAuditable — EntityId is Guid.Empty pre-creation
    public Guid EntityId => Guid.Empty;
    public string EntityType => "StaffMember";
    public string Action => "Create";
}

public sealed record StaffAvailabilityWindowInput(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CreateStaffValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreateStaffHandler(
    ITenantContext tenantContext,
    IStaffMemberRepository staffRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateStaffCommand, StaffMemberDto>
{
    public async Task<StaffMemberDto> Handle(CreateStaffCommand cmd, CancellationToken ct)
    {
        var windows = cmd.AvailabilityWindows
            .Select(w => new StaffAvailabilityWindow(w.DayOfWeek, w.StartTime, w.EndTime))
            .ToList();

        var staff = StaffMember.Create(
            tenantId: tenantContext.TenantId,
            tenantUserId: cmd.TenantUserId,
            name: cmd.Name,
            email: cmd.Email,
            availabilityWindows: windows);

        await staffRepo.AddAsync(staff, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return staff.ToDto();
    }
}
