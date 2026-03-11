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

public sealed record CancelBookingCommand : IRequest<BookingDto>, IAuditable
{
    public required Guid BookingId { get; init; }
    public required string BookingTypeSlug { get; init; }
    /// <summary>
    /// When the caller is a Customer, enforce ownership (must match Booking.CustomerId).
    /// </summary>
    public string? RequiredCustomerId { get; init; }

    // IAuditable
    public Guid EntityId => BookingId;
    public string EntityType => "Booking";
    public string Action => "Cancel";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CancelBookingValidator : AbstractValidator<CancelBookingCommand>
{
    public CancelBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.BookingTypeSlug).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CancelBookingHandler(
    ITenantContext tenantContext,
    IBookingRepository bookingRepo,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    IBookingMetrics metrics)
    : IRequestHandler<CancelBookingCommand, BookingDto>
{
    public async Task<BookingDto> Handle(CancelBookingCommand cmd, CancellationToken ct)
    {
        using var activity = ChronithActivitySource.StartBookingStateTransition("Cancel", tenantContext.TenantId, cmd.BookingId);

        var booking = await bookingRepo.GetByIdAsync(tenantContext.TenantId, cmd.BookingId, ct)
            ?? throw new NotFoundException("Booking", cmd.BookingId);

        // Customer ownership check
        if (cmd.RequiredCustomerId is not null &&
            booking.CustomerId != cmd.RequiredCustomerId)
        {
            throw new UnauthorizedAccessException(
                "Customers may only cancel their own bookings.");
        }

        var from = booking.Status;
        booking.Cancel(tenantContext.UserId, tenantContext.Role);
        await bookingRepo.UpdateAsync(booking, ct);

        metrics.RecordBookingCancelled(tenantContext.TenantId.ToString());

        await publisher.Publish(
            new Notifications.BookingStatusChangedNotification(
                BookingId: booking.Id,
                TenantId: booking.TenantId,
                BookingTypeId: booking.BookingTypeId,
                BookingTypeSlug: cmd.BookingTypeSlug,
                FromStatus: from,
                ToStatus: BookingStatus.Cancelled,
                Start: booking.Start,
                End: booking.End,
                CustomerId: booking.CustomerId,
                CustomerEmail: booking.CustomerEmail),
            ct);

        await unitOfWork.SaveChangesAsync(ct);

        return booking.ToDto();
    }
}
