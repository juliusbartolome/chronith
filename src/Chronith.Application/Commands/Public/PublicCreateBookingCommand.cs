using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Public;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record PublicCreateBookingCommand : IRequest<BookingDto>
{
    public required Guid TenantId { get; init; }
    public required string BookingTypeSlug { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required string CustomerEmail { get; init; }
    public required string CustomerId { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class PublicCreateBookingValidator : AbstractValidator<PublicCreateBookingCommand>
{
    public PublicCreateBookingValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.BookingTypeSlug).NotEmpty();
        RuleFor(x => x.StartTime).NotEmpty();
        RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class PublicCreateBookingHandler(
    IBookingTypeRepository bookingTypeRepo,
    IBookingRepository bookingRepo,
    ITenantRepository tenantRepo,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    IPaymentProviderFactory paymentProviderFactory)
    : IRequestHandler<PublicCreateBookingCommand, BookingDto>
{
    private static readonly BookingStatus[] ConflictStatuses =
    [
        BookingStatus.PendingPayment,
        BookingStatus.PendingVerification,
        BookingStatus.Confirmed
    ];

    public async Task<BookingDto> Handle(PublicCreateBookingCommand cmd, CancellationToken ct)
    {
        var bookingType = await bookingTypeRepo.GetBySlugAsync(cmd.TenantId, cmd.BookingTypeSlug, ct)
            ?? throw new NotFoundException("BookingType", cmd.BookingTypeSlug);

        var tenant = await tenantRepo.GetByIdAsync(cmd.TenantId, ct)
            ?? throw new NotFoundException("Tenant", cmd.TenantId);

        var tz = tenant.GetTimeZone();
        var (start, end) = bookingType.ResolveSlot(cmd.StartTime, tz);
        var (effStart, effEnd) = bookingType.GetEffectiveRange(start, end);

        var lockKey = BitConverter.ToInt64(bookingType.Id.ToByteArray(), 0);

        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        await tx.AcquireAdvisoryLockAsync(lockKey, ct);

        var conflictCount = await bookingRepo.CountConflictsAsync(
            bookingType.Id, effStart, effEnd, ConflictStatuses, ct);

        if (conflictCount >= bookingType.Capacity)
            throw new SlotConflictException();

        var booking = Booking.Create(
            cmd.TenantId,
            bookingType.Id,
            start,
            end,
            cmd.CustomerId,
            cmd.CustomerEmail,
            amountInCentavos: bookingType.PriceInCentavos,
            currency: bookingType.Currency);

        await bookingRepo.AddAsync(booking, ct);
        await tx.CommitAsync(ct);

        if (bookingType.PaymentMode == PaymentMode.Automatic && bookingType.PriceInCentavos > 0)
        {
            var providerName = bookingType.PaymentProvider ?? "Stub";
            var provider = paymentProviderFactory.GetProvider(providerName);
            var checkoutResult = await provider.CreateCheckoutSessionAsync(
                new CreateCheckoutRequest(
                    AmountInCentavos: bookingType.PriceInCentavos,
                    Currency: bookingType.Currency,
                    Description: $"{bookingType.Name} booking",
                    BookingId: booking.Id,
                    TenantId: cmd.TenantId),
                ct);

            booking.SetCheckoutDetails(checkoutResult.CheckoutUrl, checkoutResult.ProviderTransactionId);
            await bookingRepo.UpdateAsync(booking, ct);
        }

        await publisher.Publish(
            new Application.Notifications.BookingStatusChangedNotification(
                BookingId: booking.Id,
                TenantId: booking.TenantId,
                BookingTypeId: booking.BookingTypeId,
                BookingTypeSlug: cmd.BookingTypeSlug,
                FromStatus: null,
                ToStatus: booking.Status,
                Start: booking.Start,
                End: booking.End,
                CustomerId: booking.CustomerId,
                CustomerEmail: booking.CustomerEmail),
            ct);

        return booking.ToDto();
    }
}
