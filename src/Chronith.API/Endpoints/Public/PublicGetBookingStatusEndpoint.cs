using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Public;
using Chronith.Domain.Exceptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public;

public sealed class PublicGetBookingStatusEndpoint(ISender sender, ITenantRepository tenantRepo)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/public/{tenantSlug}/bookings/{id}");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Public"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantSlug = Route<string>("tenantSlug")!;
        var tenant = await tenantRepo.GetBySlugAsync(tenantSlug, ct)
            ?? throw new NotFoundException("Tenant", tenantSlug);

        var bookingId = Route<Guid>("id");
        var result = await sender.Send(new GetPublicBookingStatusQuery(tenant.Id, bookingId), ct);
        await Send.OkAsync(result, ct);
    }
}
