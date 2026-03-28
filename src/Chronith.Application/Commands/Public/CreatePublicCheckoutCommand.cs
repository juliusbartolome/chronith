using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
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

        RuleFor(x => x.SuccessUrl)
            .MaximumLength(2048)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http"))
            .When(x => x.SuccessUrl is not null)
            .WithMessage("SuccessUrl must be a valid absolute URL");

        RuleFor(x => x.FailureUrl)
            .MaximumLength(2048)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http"))
            .When(x => x.FailureUrl is not null)
            .WithMessage("FailureUrl must be a valid absolute URL");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreatePublicCheckoutHandler(
    IBookingRepository bookingRepo,
    ITenantRepository tenantRepo,
    ITenantPaymentProviderResolver resolver,
    IBookingUrlSigner signer,
    IOptions<PaymentPageOptions> pageOptions,
    ITenantPaymentConfigRepository configRepo,
    ILogger<CreatePublicCheckoutHandler> logger)
    : IRequestHandler<CreatePublicCheckoutCommand, CreateCheckoutResult>
{
    public async Task<CreateCheckoutResult> Handle(
        CreatePublicCheckoutCommand cmd, CancellationToken ct)
    {
        logger.LogInformation("Creating checkout for booking {BookingId}, provider {ProviderName}",
            cmd.BookingId, cmd.ProviderName);

        var tenant = await tenantRepo.GetBySlugAsync(cmd.TenantSlug, ct)
            ?? throw new NotFoundException("Tenant", cmd.TenantSlug);

        var booking = await bookingRepo.GetPublicByIdAsync(tenant.Id, cmd.BookingId, ct)
            ?? throw new NotFoundException("Booking", cmd.BookingId);

        if (booking.Status != BookingStatus.PendingPayment)
            throw new InvalidStateTransitionException(booking.Status, "create checkout");

        var provider = await resolver.ResolveAsync(tenant.Id, cmd.ProviderName, ct)
            ?? throw new NotFoundException("PaymentProvider", cmd.ProviderName);

        // --- 3-tier URL resolution ---
        var baseUrl = pageOptions.Value.BaseUrl;
        var defaultSuccessUrl = $"{baseUrl}/success";
        var defaultFailureUrl = $"{baseUrl}/failed";

        // Check tenant payment config for custom URLs
        var config = await configRepo.GetActiveByProviderNameAsync(tenant.Id, cmd.ProviderName, ct);
        var configSuccessUrl = config?.PaymentSuccessUrl;
        var configFailureUrl = config?.PaymentFailureUrl;

        // Resolution: request override > tenant config > global fallback
        var resolvedSuccessBase = cmd.SuccessUrl ?? configSuccessUrl ?? defaultSuccessUrl;
        var resolvedFailureBase = cmd.FailureUrl ?? configFailureUrl ?? defaultFailureUrl;

        var urlSource = cmd.SuccessUrl is not null || cmd.FailureUrl is not null
            ? "request override"
            : configSuccessUrl is not null || configFailureUrl is not null
                ? "tenant config"
                : "global default";
        logger.LogInformation("URL resolution: using {UrlSource} (request > tenant-config > global)", urlSource);

        // Always append HMAC signature
        var successUrl = signer.GenerateSignedUrl(resolvedSuccessBase, cmd.BookingId, cmd.TenantSlug);
        var failureUrl = signer.GenerateSignedUrl(resolvedFailureBase, cmd.BookingId, cmd.TenantSlug);

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

        logger.LogInformation("Checkout session created for booking {BookingId}, redirecting to {ProviderName}",
            cmd.BookingId, cmd.ProviderName);

        return checkoutResult;
    }
}
