using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Bookings;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CreateBookingCommand : IRequest<BookingDto>
{
    public required string BookingTypeSlug { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required string CustomerEmail { get; init; }
    public string? CustomerId { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.BookingTypeSlug).NotEmpty();
        RuleFor(x => x.StartTime).NotEmpty();
        RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreateBookingHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository bookingTypeRepo,
    IBookingRepository bookingRepo,
    ITenantRepository tenantRepo,
    IUnitOfWork unitOfWork,
    IPublisher publisher)
    : IRequestHandler<CreateBookingCommand, BookingDto>
{
    private static readonly BookingStatus[] ConflictStatuses =
    [
        BookingStatus.PendingPayment,
        BookingStatus.PendingVerification,
        BookingStatus.Confirmed
    ];

    public async Task<BookingDto> Handle(CreateBookingCommand cmd, CancellationToken ct)
    {
        var bookingType = await bookingTypeRepo.GetBySlugAsync(tenantContext.TenantId, cmd.BookingTypeSlug, ct)
            ?? throw new NotFoundException("BookingType", cmd.BookingTypeSlug);

        var tenant = await tenantRepo.GetByIdAsync(tenantContext.TenantId, ct)
            ?? throw new NotFoundException("Tenant", tenantContext.TenantId);

        var tz = tenant.GetTimeZone();
        var (start, end) = bookingType.ResolveSlot(cmd.StartTime, tz);
        var (effStart, effEnd) = bookingType.GetEffectiveRange(start, end);

        // Stub payment reference for Automatic payment mode
        string? paymentRef = bookingType.PaymentMode == PaymentMode.Automatic
            ? Guid.NewGuid().ToString()
            : null;

        // Use the lower 64 bits of the booking type ID as a stable advisory lock key.
        // This serializes all concurrent create attempts for the same booking type,
        // eliminating the TOCTOU race between CountConflictsAsync and AddAsync.
        var lockKey = BitConverter.ToInt64(bookingType.Id.ToByteArray(), 0);

        await using var tx = await unitOfWork.BeginTransactionAsync(ct);

        await tx.AcquireAdvisoryLockAsync(lockKey, ct);

        // COUNT conflict query runs as SQL — now protected by advisory lock
        var conflictCount = await bookingRepo.CountConflictsAsync(
            bookingType.Id, effStart, effEnd, ConflictStatuses, ct);

        if (conflictCount >= bookingType.Capacity)
            throw new SlotConflictException();

        var customerId = cmd.CustomerId ?? tenantContext.UserId;

        var booking = Booking.Create(
            tenantContext.TenantId,
            bookingType.Id,
            start,
            end,
            customerId,
            cmd.CustomerEmail,
            paymentRef);

        await bookingRepo.AddAsync(booking, ct);
        await tx.CommitAsync(ct);

        await publisher.Publish(
            new Notifications.BookingStatusChangedNotification(booking.Id, null, BookingStatus.PendingPayment),
            ct);

        return booking.ToDto();
    }
}
