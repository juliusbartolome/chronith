using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Public;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record ConfirmManualPaymentCommand : IRequest<PublicBookingStatusDto>
{
    public required string TenantSlug { get; init; }
    public required Guid BookingId { get; init; }
    public required long Expires { get; init; }
    public required string Signature { get; init; }
    public Stream? ProofFile { get; init; }
    public string? ProofFileName { get; init; }
    public string? ProofContentType { get; init; }
    public string? PaymentNote { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class ConfirmManualPaymentCommandValidator : AbstractValidator<ConfirmManualPaymentCommand>
{
    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp"];

    public ConfirmManualPaymentCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty();
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Expires).GreaterThan(0);
        RuleFor(x => x.Signature).NotEmpty();
        RuleFor(x => x.PaymentNote).MaximumLength(500);

        RuleFor(x => x.ProofContentType)
            .Must(ct => AllowedContentTypes.Contains(ct))
            .When(x => x.ProofFile is not null)
            .WithMessage("Proof file must be image/jpeg, image/png, or image/webp.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ConfirmManualPaymentCommandHandler(
    IBookingUrlSigner signer,
    ITenantRepository tenantRepo,
    IBookingRepository bookingRepo,
    IBookingTypeRepository bookingTypeRepo,
    ITenantPaymentConfigRepository paymentConfigRepo,
    IFileStorageService fileStorage,
    IUnitOfWork unitOfWork,
    IPublisher publisher)
    : IRequestHandler<ConfirmManualPaymentCommand, PublicBookingStatusDto>
{
    public async Task<PublicBookingStatusDto> Handle(
        ConfirmManualPaymentCommand cmd, CancellationToken ct)
    {
        // 1. Validate HMAC signature
        if (!signer.Validate(cmd.BookingId, cmd.TenantSlug, cmd.Expires, cmd.Signature))
            throw new UnauthorizedException("Invalid or expired booking access token.");

        // 2. Resolve tenant
        var tenant = await tenantRepo.GetBySlugAsync(cmd.TenantSlug, ct)
            ?? throw new NotFoundException("Tenant", cmd.TenantSlug);

        // 3. Get booking (public, cross-tenant via tenant ID)
        var booking = await bookingRepo.GetPublicByIdAsync(tenant.Id, cmd.BookingId, ct)
            ?? throw new NotFoundException("Booking", cmd.BookingId);

        // 4. Upload proof file if provided
        string? proofUrl = null;
        string? proofFileName = null;
        if (cmd.ProofFile is not null)
        {
            var containerName = $"payment-proofs-{cmd.TenantSlug}";
            var result = await fileStorage.UploadAsync(
                containerName, cmd.ProofFileName ?? "proof", cmd.ProofFile, cmd.ProofContentType ?? "image/jpeg", ct);
            proofUrl = result.Url;
            proofFileName = result.FileName;
        }

        // 5. Transition booking state (throws InvalidStateTransitionException if not PendingPayment)
        var fromStatus = booking.Status;
        booking.SubmitProofOfPayment(proofUrl, proofFileName, cmd.PaymentNote, "customer", "customer");

        // 6. Persist
        await bookingRepo.UpdateAsync(booking, ct);

        // 7. Load booking type for notification slug and DTO
        var bookingType = await bookingTypeRepo.GetByIdAsync(booking.BookingTypeId, ct);

        // 8. Publish notification
        await publisher.Publish(
            new Notifications.BookingStatusChangedNotification(
                BookingId: booking.Id,
                TenantId: tenant.Id,
                BookingTypeId: booking.BookingTypeId,
                BookingTypeSlug: bookingType?.Slug ?? string.Empty,
                FromStatus: fromStatus,
                ToStatus: BookingStatus.PendingVerification,
                Start: booking.Start,
                End: booking.End,
                CustomerId: booking.CustomerId,
                CustomerEmail: booking.CustomerEmail,
                CustomerFirstName: booking.FirstName,
                CustomerLastName: booking.LastName,
                CustomerMobile: booking.Mobile),
            ct);

        // 9. Commit unit of work
        await unitOfWork.SaveChangesAsync(ct);

        // 10. Return enriched DTO
        return await PublicBookingStatusMapper.ToPublicStatusDtoAsync(
            booking, bookingTypeRepo, paymentConfigRepo, tenant.Id, ct);
    }
}
