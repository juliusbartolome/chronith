using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Chronith.Application.Commands.Public;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CreatePublicCheckoutCommand : IRequest<CreateCheckoutResult>
{
    public required string TenantSlug { get; init; }
    public required Guid BookingId { get; init; }
    public required string ProviderName { get; init; }
    public string? SuccessUrl { get; init; }
    public string? FailureUrl { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CreatePublicCheckoutValidator : AbstractValidator<CreatePublicCheckoutCommand>
{
    public CreatePublicCheckoutValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty();
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.ProviderName).NotEmpty().MaximumLength(50);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreatePublicCheckoutHandler(
    IBookingRepository bookingRepo,
    ITenantRepository tenantRepo,
    ITenantPaymentProviderResolver resolver,
    IBookingUrlSigner signer,
    IOptions<PaymentPageOptions> pageOptions)
    : IRequestHandler<CreatePublicCheckoutCommand, CreateCheckoutResult>
{
    public async Task<CreateCheckoutResult> Handle(
        CreatePublicCheckoutCommand cmd, CancellationToken ct)
    {
        var tenant = await tenantRepo.GetBySlugAsync(cmd.TenantSlug, ct)
            ?? throw new NotFoundException("Tenant", cmd.TenantSlug);

        var booking = await bookingRepo.GetPublicByIdAsync(tenant.Id, cmd.BookingId, ct)
            ?? throw new NotFoundException("Booking", cmd.BookingId);

        if (booking.Status != BookingStatus.PendingPayment)
            throw new InvalidStateTransitionException(booking.Status, "create checkout");

        var provider = await resolver.ResolveAsync(tenant.Id, cmd.ProviderName, ct)
            ?? throw new NotFoundException("PaymentProvider", cmd.ProviderName);

        var baseUrl = pageOptions.Value.BaseUrl;
        var successUrl = signer.GenerateSignedUrl($"{baseUrl}/success", cmd.BookingId, cmd.TenantSlug);
        var failureUrl = signer.GenerateSignedUrl($"{baseUrl}/failed", cmd.BookingId, cmd.TenantSlug);

        var checkoutResult = await provider.CreateCheckoutSessionAsync(
            new CreateCheckoutRequest(
                AmountInCentavos: booking.AmountInCentavos,
                Currency: booking.Currency,
                Description: $"Booking {booking.Id}",
                BookingId: booking.Id,
                TenantId: tenant.Id,
                SuccessUrl: successUrl,
                CancelUrl: failureUrl),
            ct);

        booking.SetCheckoutDetails(checkoutResult.CheckoutUrl, checkoutResult.ProviderTransactionId);
        await bookingRepo.UpdateAsync(booking, ct);

        return checkoutResult;
    }
}
