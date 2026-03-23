using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Public;
using Chronith.Domain.Exceptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public;

public sealed class PublicVerifyBookingRequest
{
    public string TenantSlug { get; set; } = string.Empty;
    public Guid BookingId { get; set; }

    [QueryParam]
    public long Expires { get; set; }

    [QueryParam]
    public string Sig { get; set; } = string.Empty;
}

public sealed class PublicVerifyBookingEndpoint(
    ISender sender,
    ITenantRepository tenantRepo,
    IBookingUrlSigner signer)
    : Endpoint<PublicVerifyBookingRequest, PublicBookingStatusDto>
{
    public override void Configure()
    {
        Get("/public/{tenantSlug}/bookings/{bookingId}/verify");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Public"));
    }

    public override async Task HandleAsync(PublicVerifyBookingRequest req, CancellationToken ct)
    {
        if (!signer.Validate(req.BookingId, req.TenantSlug, req.Expires, req.Sig))
            throw new UnauthorizedException("Invalid or expired booking access token.");

        var tenant = await tenantRepo.GetBySlugAsync(req.TenantSlug, ct)
            ?? throw new NotFoundException("Tenant", req.TenantSlug);

        var result = await sender.Send(
            new GetVerifiedBookingQuery(tenant.Id, req.BookingId), ct);

        await Send.OkAsync(result, ct);
    }
}
