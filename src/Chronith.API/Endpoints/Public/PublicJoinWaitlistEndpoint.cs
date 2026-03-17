using Chronith.Application.Commands.Public;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public;

public sealed class PublicJoinWaitlistRequest
{
    public string TenantSlug { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public DateTimeOffset DesiredStart { get; set; }
    public DateTimeOffset DesiredEnd { get; set; }
}

public sealed class PublicJoinWaitlistEndpoint(ISender sender, ITenantRepository tenantRepo)
    : Endpoint<PublicJoinWaitlistRequest, WaitlistEntryDto>
{
    public override void Configure()
    {
        Post("/public/{tenantSlug}/booking-types/{slug}/waitlist");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Public"));
    }

    public override async Task HandleAsync(PublicJoinWaitlistRequest req, CancellationToken ct)
    {
        var tenant = await tenantRepo.GetBySlugAsync(req.TenantSlug, ct)
            ?? throw new NotFoundException("Tenant", req.TenantSlug);

        var result = await sender.Send(new PublicJoinWaitlistCommand
        {
            TenantId = tenant.Id,
            BookingTypeSlug = req.Slug,
            CustomerId = req.CustomerId,
            CustomerEmail = req.CustomerEmail,
            DesiredStart = req.DesiredStart,
            DesiredEnd = req.DesiredEnd
        }, ct);

        await Send.ResponseAsync(result, 201, ct);
    }
}
