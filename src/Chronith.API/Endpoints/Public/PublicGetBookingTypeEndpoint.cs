using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Public;
using Chronith.Domain.Exceptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public;

public sealed class PublicGetBookingTypeEndpoint(ISender sender, ITenantRepository tenantRepo)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/public/{tenantSlug}/booking-types/{slug}");
        AllowAnonymous();
        Options(x => x.WithTags("Public"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantSlug = Route<string>("tenantSlug")!;
        var tenant = await tenantRepo.GetBySlugAsync(tenantSlug, ct)
            ?? throw new NotFoundException("Tenant", tenantSlug);

        var slug = Route<string>("slug")!;
        var result = await sender.Send(new PublicGetBookingTypeQuery(tenant.Id, slug), ct);
        await Send.OkAsync(result, ct);
    }
}
