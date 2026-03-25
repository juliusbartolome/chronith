using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Application.Options;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Chronith.Application.Commands.Public;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record PublicCreateBookingCommand : IRequest<BookingDto>
{
    public required Guid TenantId { get; init; }
    public required string BookingTypeSlug { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required string CustomerEmail { get; init; }
    public required string CustomerId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Mobile { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class PublicCreateBookingValidator : AbstractValidator<PublicCreateBookingCommand>
{
    public PublicCreateBookingValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.BookingTypeSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StartTime).NotEmpty();
        RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FirstName).MaximumLength(200).When(x => x.FirstName is not null);
        RuleFor(x => x.LastName).MaximumLength(200).When(x => x.LastName is not null);
        RuleFor(x => x.Mobile).MaximumLength(50).When(x => x.Mobile is not null);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class PublicCreateBookingHandler(
    IBookingTypeRepository bookingTypeRepo,
    IBookingRepository bookingRepo,
    ITenantRepository tenantRepo,
    ICustomerRepository customerRepo,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    IBookingUrlSigner signer,
    IOptions<PaymentPageOptions> pageOptions)
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
            currency: bookingType.Currency,
            firstName: cmd.FirstName,
            lastName: cmd.LastName,
            mobile: cmd.Mobile);

        // Customer upsert: only when contact fields are provided
        var hasContactFields = !string.IsNullOrWhiteSpace(cmd.FirstName)
            || !string.IsNullOrWhiteSpace(cmd.LastName)
            || !string.IsNullOrWhiteSpace(cmd.Mobile);

        if (hasContactFields)
        {
            var existing = await customerRepo.GetByEmailAsync(cmd.TenantId, cmd.CustomerEmail, ct);
            if (existing is not null)
            {
                existing.UpdateProfile(
                    cmd.FirstName ?? existing.FirstName,
                    cmd.LastName ?? existing.LastName,
                    cmd.Mobile ?? existing.Mobile);
                customerRepo.Update(existing);
                booking.LinkCustomerAccount(existing.Id);
            }
            else
            {
                var customer = Customer.Create(
                    cmd.TenantId,
                    cmd.CustomerEmail,
                    passwordHash: null,
                    firstName: cmd.FirstName ?? string.Empty,
                    lastName: cmd.LastName ?? string.Empty,
                    mobile: cmd.Mobile,
                    authProvider: "public");
                await customerRepo.AddAsync(customer, ct);
                booking.LinkCustomerAccount(customer.Id);
            }
        }

        await bookingRepo.AddAsync(booking, ct);
        await tx.CommitAsync(ct);

        // For any non-free booking, generate HMAC-signed payment URL.
        // Checkout sessions are created on-demand when the customer picks a provider.
        string? paymentUrl = null;
        if (bookingType.PriceInCentavos > 0)
        {
            paymentUrl = signer.GenerateSignedUrl(
                pageOptions.Value.BaseUrl, booking.Id, tenant.Slug);
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
                CustomerEmail: booking.CustomerEmail,
                CustomerFirstName: booking.FirstName,
                CustomerLastName: booking.LastName,
                CustomerMobile: booking.Mobile),
            ct);

        return booking.ToDto(paymentUrl);
    }
}
