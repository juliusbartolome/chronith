using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Public;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record VerifyBookingPaymentCommand : IRequest<PublicBookingStatusDto>
{
    public required string TenantSlug { get; init; }
    public required Guid BookingId { get; init; }
    public required long Expires { get; init; }
    public required string Signature { get; init; }
    public required string Action { get; init; } // "approve" or "reject"
    public string? Note { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class VerifyBookingPaymentCommandValidator : AbstractValidator<VerifyBookingPaymentCommand>
{
    private static readonly string[] ValidActions = ["approve", "reject"];

    public VerifyBookingPaymentCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty();
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Expires).GreaterThan(0);
        RuleFor(x => x.Signature).NotEmpty();
        RuleFor(x => x.Action)
            .NotEmpty()
            .Must(a => ValidActions.Contains(a))
            .WithMessage("Action must be 'approve' or 'reject'.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class VerifyBookingPaymentCommandHandler(
    IBookingUrlSigner signer,
    ITenantRepository tenantRepo,
    IBookingRepository bookingRepo,
    IBookingTypeRepository bookingTypeRepo,
    ITenantPaymentConfigRepository paymentConfigRepo,
    IUnitOfWork unitOfWork,
    IPublisher publisher)
    : IRequestHandler<VerifyBookingPaymentCommand, PublicBookingStatusDto>
{
    public async Task<PublicBookingStatusDto> Handle(
        VerifyBookingPaymentCommand cmd, CancellationToken ct)
    {
        // 1. Validate HMAC signature (staff-verify domain)
        if (!signer.ValidateStaffVerify(cmd.BookingId, cmd.TenantSlug, cmd.Expires, cmd.Signature))
            throw new UnauthorizedException("Invalid or expired staff verification token.");

        // 2. Resolve tenant
        var tenant = await tenantRepo.GetBySlugAsync(cmd.TenantSlug, ct)
            ?? throw new NotFoundException("Tenant", cmd.TenantSlug);

        // 3. Get booking (public, cross-tenant via tenant ID)
        var booking = await bookingRepo.GetPublicByIdAsync(tenant.Id, cmd.BookingId, ct)
            ?? throw new NotFoundException("Booking", cmd.BookingId);

        // 4. Transition booking state based on action
        // TODO: Wire cmd.Note into notification payload (Task 7) or store on booking
        var fromStatus = booking.Status;
        BookingStatus toStatus;

        if (cmd.Action == "approve")
        {
            booking.Confirm("staff", "staff");
            toStatus = BookingStatus.Confirmed;
        }
        else // "reject"
        {
            booking.Cancel("staff", "staff");
            toStatus = BookingStatus.Cancelled;
        }

        // 5. Persist (use public update — bypasses tenant query filter for anonymous endpoints)
        await bookingRepo.UpdatePublicAsync(booking, tenant.Id, ct);

        // 6. Load booking type for notification slug and DTO
        var bookingType = await bookingTypeRepo.GetByIdAsync(booking.BookingTypeId, ct);

        // 7. Publish notification
        await publisher.Publish(
            new Notifications.BookingStatusChangedNotification(
                BookingId: booking.Id,
                TenantId: tenant.Id,
                BookingTypeId: booking.BookingTypeId,
                BookingTypeSlug: bookingType?.Slug ?? string.Empty,
                FromStatus: fromStatus,
                ToStatus: toStatus,
                Start: booking.Start,
                End: booking.End,
                CustomerId: booking.CustomerId,
                CustomerEmail: booking.CustomerEmail,
                CustomerFirstName: booking.FirstName,
                CustomerLastName: booking.LastName,
                CustomerMobile: booking.Mobile),
            ct);

        // 8. Commit unit of work
        await unitOfWork.SaveChangesAsync(ct);

        // 9. Return enriched DTO
        return await PublicBookingStatusMapper.ToPublicStatusDtoAsync(
            booking, bookingTypeRepo, paymentConfigRepo, tenant.Id, ct);
    }
}
