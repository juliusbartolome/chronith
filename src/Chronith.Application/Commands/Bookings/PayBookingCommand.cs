using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Bookings;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record PayBookingCommand : IRequest<BookingDto>
{
    public required Guid BookingId { get; init; }
    public required string BookingTypeSlug { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class PayBookingValidator : AbstractValidator<PayBookingCommand>
{
    public PayBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.BookingTypeSlug).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class PayBookingHandler(
    ITenantContext tenantContext,
    IBookingRepository bookingRepo,
    IUnitOfWork unitOfWork,
    IPublisher publisher)
    : IRequestHandler<PayBookingCommand, BookingDto>
{
    public async Task<BookingDto> Handle(PayBookingCommand cmd, CancellationToken ct)
    {
        var booking = await bookingRepo.GetByIdAsync(tenantContext.TenantId, cmd.BookingId, ct)
            ?? throw new NotFoundException("Booking", cmd.BookingId);

        var from = booking.Status;
        booking.Pay(tenantContext.UserId, tenantContext.Role);
        await unitOfWork.SaveChangesAsync(ct);

        await publisher.Publish(
            new Notifications.BookingStatusChangedNotification(booking.Id, from, BookingStatus.PendingVerification),
            ct);

        return booking.ToDto();
    }
}
