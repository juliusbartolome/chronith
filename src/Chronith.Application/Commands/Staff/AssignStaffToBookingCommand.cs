using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Staff;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record AssignStaffToBookingCommand : IRequest<BookingDto>, IAuditable
{
    public required Guid BookingId { get; init; }
    public required Guid StaffMemberId { get; init; }

    public Guid EntityId => BookingId;
    public string EntityType => "Booking";
    public string Action => "AssignStaff";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class AssignStaffToBookingValidator : AbstractValidator<AssignStaffToBookingCommand>
{
    public AssignStaffToBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.StaffMemberId).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class AssignStaffToBookingHandler(
    ITenantContext tenantContext,
    IBookingRepository bookingRepo,
    IStaffMemberRepository staffRepo,
    IBookingTypeRepository bookingTypeRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AssignStaffToBookingCommand, BookingDto>
{
    public async Task<BookingDto> Handle(AssignStaffToBookingCommand cmd, CancellationToken ct)
    {
        var booking = await bookingRepo.GetByIdAsync(tenantContext.TenantId, cmd.BookingId, ct)
            ?? throw new NotFoundException("Booking", cmd.BookingId);

        var staff = await staffRepo.GetByIdAsync(tenantContext.TenantId, cmd.StaffMemberId, ct)
            ?? throw new NotFoundException("StaffMember", cmd.StaffMemberId);

        if (!staff.IsActive)
            throw new ConflictException("StaffMember is not active.");

        // If booking type requires staff assignment, validate staff is assigned to that booking type
        var bookingType = await bookingTypeRepo.GetByIdAsync(tenantContext.TenantId, booking.BookingTypeId, ct);
        if (bookingType is not null && bookingType.RequiresStaffAssignment)
        {
            var assignedStaff = await staffRepo.ListByBookingTypeAsync(
                tenantContext.TenantId, booking.BookingTypeId, ct);

            if (!assignedStaff.Any(s => s.Id == cmd.StaffMemberId))
                throw new ConflictException("StaffMember is not assigned to this booking type.");
        }

        booking.AssignStaff(cmd.StaffMemberId, tenantContext.UserId, tenantContext.Role);
        await bookingRepo.UpdateAsync(booking, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return booking.ToDto();
    }
}
