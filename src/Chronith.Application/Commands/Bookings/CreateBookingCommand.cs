using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Application.Telemetry;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Bookings;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CreateBookingCommand : IRequest<BookingDto>, IAuditable, IPlanEnforcedCommand
{
    public required string BookingTypeSlug { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required string CustomerEmail { get; init; }
    public string? CustomerId { get; init; }

    // IAuditable — EntityId is Guid.Empty pre-creation; AuditBehavior captures newValues post-handler
    public Guid EntityId => Guid.Empty;
    public string EntityType => "Booking";
    public string Action => "Create";

    // IPlanEnforcedCommand
    public string EnforcedResourceType => "Booking";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.BookingTypeSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StartTime).NotEmpty();
        RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreateBookingHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository bookingTypeRepo,
    IBookingRepository bookingRepo,
    ITenantRepository tenantRepo,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    IPaymentProviderFactory paymentProviderFactory,
    IBookingMetrics metrics)
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
        using var activity = ChronithActivitySource.StartBookingStateTransition("Create", tenantContext.TenantId, Guid.Empty);

        var bookingType = await bookingTypeRepo.GetBySlugAsync(tenantContext.TenantId, cmd.BookingTypeSlug, ct)
            ?? throw new NotFoundException("BookingType", cmd.BookingTypeSlug);

        var tenant = await tenantRepo.GetByIdAsync(tenantContext.TenantId, ct)
            ?? throw new NotFoundException("Tenant", tenantContext.TenantId);

        var tz = tenant.GetTimeZone();
        var (start, end) = bookingType.ResolveSlot(cmd.StartTime, tz);
        var (effStart, effEnd) = bookingType.GetEffectiveRange(start, end);

        var customerId = cmd.CustomerId ?? tenantContext.UserId;

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

        var booking = Booking.Create(
            tenantContext.TenantId,
            bookingType.Id,
            start,
            end,
            customerId,
            cmd.CustomerEmail,
            amountInCentavos: bookingType.PriceInCentavos,
            currency: bookingType.Currency);

        await bookingRepo.AddAsync(booking, ct);
        await tx.CommitAsync(ct);

        activity?.SetTag("booking.id", booking.Id.ToString());

        metrics.RecordBookingCreated(
            tenantContext.TenantId.ToString(),
            bookingType is TimeSlotBookingType ? "TimeSlot" : "Calendar");

        // For Automatic payment mode with a non-free booking, create a checkout session
        if (bookingType.PaymentMode == PaymentMode.Automatic && bookingType.PriceInCentavos > 0)
        {
            var providerName = bookingType.PaymentProvider ?? "Stub";
            using var payActivity = ChronithActivitySource.StartPaymentProcess(tenantContext.TenantId, providerName);
            var provider = paymentProviderFactory.GetProvider(providerName);
            var checkoutResult = await provider.CreateCheckoutSessionAsync(
                new CreateCheckoutRequest(
                    AmountInCentavos: bookingType.PriceInCentavos,
                    Currency: bookingType.Currency,
                    Description: $"{bookingType.Name} booking",
                    BookingId: booking.Id,
                    TenantId: tenantContext.TenantId),
                ct);

            booking.SetCheckoutDetails(checkoutResult.CheckoutUrl, checkoutResult.ProviderTransactionId);

            metrics.RecordPaymentProcessed(tenantContext.TenantId.ToString(), providerName);

            // Persist the updated PaymentReference and CheckoutUrl. The booking was
            // committed inside the advisory-lock transaction above, so the tracked entity
            // in the DbContext does not reflect these in-memory changes. Using UpdateAsync
            // (which issues an ExecuteUpdateAsync SQL statement directly) ensures both
            // fields are written to the database in a second round-trip.
            await bookingRepo.UpdateAsync(booking, ct);
        }

        await publisher.Publish(
            new Notifications.BookingStatusChangedNotification(
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
