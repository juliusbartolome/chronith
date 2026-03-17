using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Public;
using Chronith.Domain.Exceptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public;

public sealed class PublicGetAvailabilityRequest
{
    public string TenantSlug { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    [QueryParam]
    public DateTimeOffset From { get; set; }

    [QueryParam]
    public DateTimeOffset To { get; set; }
}

public sealed class PublicGetAvailabilityEndpoint(ISender sender, ITenantRepository tenantRepo)
    : Endpoint<PublicGetAvailabilityRequest, AvailabilityDto>
{
    public override void Configure()
    {
        Get("/public/{tenantSlug}/booking-types/{slug}/availability");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Public"));
    }

    public override async Task HandleAsync(PublicGetAvailabilityRequest req, CancellationToken ct)
    {
        var tenant = await tenantRepo.GetBySlugAsync(req.TenantSlug, ct)
            ?? throw new NotFoundException("Tenant", req.TenantSlug);

        var result = await sender.Send(new PublicGetAvailabilityQuery
        {
            TenantId = tenant.Id,
            BookingTypeSlug = req.Slug,
            From = req.From,
            To = req.To
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
