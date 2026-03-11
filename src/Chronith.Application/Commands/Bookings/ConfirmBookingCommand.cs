using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Application.Telemetry;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Bookings;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record ConfirmBookingCommand : IRequest<BookingDto>, IAuditable
{
    public required Guid BookingId { get; init; }
    public required string BookingTypeSlug { get; init; }

    // IAuditable
    public Guid EntityId => BookingId;
    public string EntityType => "Booking";
    public string Action => "Confirm";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class ConfirmBookingValidator : AbstractValidator<ConfirmBookingCommand>
{
    public ConfirmBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.BookingTypeSlug).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ConfirmBookingHandler(
    ITenantContext tenantContext,
    IBookingRepository bookingRepo,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    IBookingMetrics metrics)
    : IRequestHandler<ConfirmBookingCommand, BookingDto>
{
    public async Task<BookingDto> Handle(ConfirmBookingCommand cmd, CancellationToken ct)
    {
        using var activity = ChronithActivitySource.StartBookingStateTransition("Confirm", tenantContext.TenantId, cmd.BookingId);

        var booking = await bookingRepo.GetByIdAsync(tenantContext.TenantId, cmd.BookingId, ct)
            ?? throw new NotFoundException("Booking", cmd.BookingId);

        var from = booking.Status;
        booking.Confirm(tenantContext.UserId, tenantContext.Role);
        await bookingRepo.UpdateAsync(booking, ct);

        metrics.RecordBookingConfirmed(tenantContext.TenantId.ToString());

        await publisher.Publish(
            new Notifications.BookingStatusChangedNotification(
                BookingId: booking.Id,
                TenantId: booking.TenantId,
                BookingTypeId: booking.BookingTypeId,
                BookingTypeSlug: cmd.BookingTypeSlug,
                FromStatus: from,
                ToStatus: BookingStatus.Confirmed,
                Start: booking.Start,
                End: booking.End,
                CustomerId: booking.CustomerId,
                CustomerEmail: booking.CustomerEmail),
            ct);

        await unitOfWork.SaveChangesAsync(ct);

        return booking.ToDto();
    }
}
