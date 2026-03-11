using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Public;
using Chronith.Domain.Exceptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public;

public sealed class PublicGetStaffAvailabilityRequest
{
    public string TenantSlug { get; set; } = string.Empty;
    public Guid Id { get; set; }

    [QueryParam]
    public DateTimeOffset From { get; set; }

    [QueryParam]
    public DateTimeOffset To { get; set; }
}

public sealed class PublicGetStaffAvailabilityEndpoint(ISender sender, ITenantRepository tenantRepo)
    : Endpoint<PublicGetStaffAvailabilityRequest, AvailabilityDto>
{
    public override void Configure()
    {
        Get("/public/{tenantSlug}/staff/{id}/availability");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Public"));
    }

    public override async Task HandleAsync(PublicGetStaffAvailabilityRequest req, CancellationToken ct)
    {
        var tenant = await tenantRepo.GetBySlugAsync(req.TenantSlug, ct)
            ?? throw new NotFoundException("Tenant", req.TenantSlug);

        var result = await sender.Send(new PublicGetStaffAvailabilityQuery
        {
            TenantId = tenant.Id,
            StaffId = req.Id,
            From = req.From,
            To = req.To
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
