using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Bookings;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record RescheduleBookingCommand : IRequest<BookingDto>, IAuditable
{
    public required Guid BookingId { get; init; }
    public required DateTimeOffset NewStart { get; init; }
    public required DateTimeOffset NewEnd { get; init; }
    /// <summary>
    /// When the caller is a Customer, enforce ownership.
    /// </summary>
    public string? RequiredCustomerId { get; init; }

    // IAuditable
    public Guid EntityId => BookingId;
    public string EntityType => "Booking";
    public string Action => "Reschedule";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class RescheduleBookingValidator : AbstractValidator<RescheduleBookingCommand>
{
    public RescheduleBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.NewEnd).GreaterThan(x => x.NewStart);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class RescheduleBookingHandler(
    ITenantContext tenantContext,
    IBookingRepository bookingRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RescheduleBookingCommand, BookingDto>
{
    public async Task<BookingDto> Handle(RescheduleBookingCommand cmd, CancellationToken ct)
    {
        var booking = await bookingRepo.GetByIdAsync(tenantContext.TenantId, cmd.BookingId, ct)
            ?? throw new NotFoundException("Booking", cmd.BookingId);

        if (cmd.RequiredCustomerId is not null && booking.CustomerId != cmd.RequiredCustomerId)
            throw new UnauthorizedAccessException("Customers may only reschedule their own bookings.");

        booking.Reschedule(cmd.NewStart, cmd.NewEnd, tenantContext.UserId, tenantContext.Role);
        await bookingRepo.UpdateAsync(booking, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return booking.ToDto();
    }
}
