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

public sealed record PayBookingCommand : IRequest<BookingDto>, IAuditable
{
    public required Guid BookingId { get; init; }
    public string? PaymentReference { get; init; }

    // IAuditable
    public Guid EntityId => BookingId;
    public string EntityType => "Booking";
    public string Action => "Pay";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class PayBookingValidator : AbstractValidator<PayBookingCommand>
{
    public PayBookingValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class PayBookingHandler(
    ITenantContext tenantContext,
    IBookingRepository bookingRepo,
    IBookingTypeRepository bookingTypeRepo,
    IUnitOfWork unitOfWork,
    IPublisher publisher)
    : IRequestHandler<PayBookingCommand, BookingDto>
{
    public async Task<BookingDto> Handle(PayBookingCommand cmd, CancellationToken ct)
    {
        using var activity = ChronithActivitySource.StartBookingStateTransition("Pay", tenantContext.TenantId, cmd.BookingId);

        var booking = await bookingRepo.GetByIdAsync(tenantContext.TenantId, cmd.BookingId, ct)
            ?? throw new NotFoundException("Booking", cmd.BookingId);

        var bookingType = await bookingTypeRepo.GetByIdAsync(tenantContext.TenantId, booking.BookingTypeId, ct)
            ?? throw new NotFoundException("BookingType", booking.BookingTypeId);

        if (cmd.PaymentReference is not null)
            booking.SetPaymentReference(cmd.PaymentReference);

        var from = booking.Status;
        booking.Pay(tenantContext.UserId, tenantContext.Role);
        await bookingRepo.UpdateAsync(booking, ct);

        await publisher.Publish(
            new Notifications.BookingStatusChangedNotification(
                BookingId: booking.Id,
                TenantId: booking.TenantId,
                BookingTypeId: booking.BookingTypeId,
                BookingTypeSlug: bookingType.Slug,
                FromStatus: from,
                ToStatus: BookingStatus.PendingVerification,
                Start: booking.Start,
                End: booking.End,
                CustomerId: booking.CustomerId,
                CustomerEmail: booking.CustomerEmail,
                CustomerFirstName: booking.FirstName,
                CustomerLastName: booking.LastName,
                CustomerMobile: booking.Mobile),
            ct);

        await unitOfWork.SaveChangesAsync(ct);

        return booking.ToDto();
    }
}
