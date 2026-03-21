using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Bookings;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record DeleteBookingCommand(Guid BookingId) : IRequest;

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class DeleteBookingValidator : AbstractValidator<DeleteBookingCommand>
{
    public DeleteBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class DeleteBookingHandler(
    ITenantContext tenantContext,
    IBookingRepository bookingRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteBookingCommand>
{
    public async Task Handle(DeleteBookingCommand cmd, CancellationToken ct)
    {
        var booking = await bookingRepo.GetByIdAsync(tenantContext.TenantId, cmd.BookingId, ct)
            ?? throw new NotFoundException("Booking", cmd.BookingId);

        booking.SoftDelete();
        await bookingRepo.UpdateAsync(booking, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
