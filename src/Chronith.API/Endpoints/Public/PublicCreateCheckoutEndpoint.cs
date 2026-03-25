using Chronith.Application.Commands.Public;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public;

public sealed class PublicCreateCheckoutRequest
{
    public string TenantSlug { get; set; } = string.Empty;
    public Guid BookingId { get; set; }

    [QueryParam]
    public long Expires { get; set; }

    [QueryParam]
    public string Sig { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;
    public string? SuccessUrl { get; set; }
    public string? FailureUrl { get; set; }
}

public sealed class PublicCreateCheckoutEndpoint(
    ISender sender,
    IBookingUrlSigner signer)
    : Endpoint<PublicCreateCheckoutRequest, CreateCheckoutResult>
{
    public override void Configure()
    {
        Post("/public/{tenantSlug}/bookings/{bookingId}/checkout");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Public"));
    }

    public override async Task HandleAsync(PublicCreateCheckoutRequest req, CancellationToken ct)
    {
        if (!signer.Validate(req.BookingId, req.TenantSlug, req.Expires, req.Sig))
            throw new UnauthorizedException("Invalid or expired booking access token.");

        var result = await sender.Send(new CreatePublicCheckoutCommand
        {
            TenantSlug = req.TenantSlug,
            BookingId = req.BookingId,
            ProviderName = req.ProviderName,
            SuccessUrl = req.SuccessUrl,
            FailureUrl = req.FailureUrl
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
