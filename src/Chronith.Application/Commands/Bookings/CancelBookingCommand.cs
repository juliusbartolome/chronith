using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Bookings;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CancelBookingCommand : IRequest<BookingDto>
{
    public required Guid BookingId { get; init; }
    public required string BookingTypeSlug { get; init; }
    /// <summary>
    /// When the caller is a Customer, enforce ownership (must match Booking.CustomerId).
    /// </summary>
    public string? RequiredCustomerId { get; init; }
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
    IPublisher publisher)
    : IRequestHandler<CancelBookingCommand, BookingDto>
{
    public async Task<BookingDto> Handle(CancelBookingCommand cmd, CancellationToken ct)
    {
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
        await unitOfWork.SaveChangesAsync(ct);

        await publisher.Publish(
            new Notifications.BookingStatusChangedNotification(booking.Id, from, BookingStatus.Cancelled),
            ct);

        return booking.ToDto();
    }
}
