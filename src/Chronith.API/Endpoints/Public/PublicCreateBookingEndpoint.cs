using Chronith.Application.Commands.Public;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public;

public sealed class PublicCreateBookingRequest
{
    public string TenantSlug { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
}

public sealed class PublicCreateBookingEndpoint(ISender sender, ITenantRepository tenantRepo)
    : Endpoint<PublicCreateBookingRequest, BookingDto>
{
    public override void Configure()
    {
        Post("/public/{tenantSlug}/booking-types/{slug}/bookings");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Public"));
    }

    public override async Task HandleAsync(PublicCreateBookingRequest req, CancellationToken ct)
    {
        var tenant = await tenantRepo.GetBySlugAsync(req.TenantSlug, ct)
            ?? throw new NotFoundException("Tenant", req.TenantSlug);

        var result = await sender.Send(new PublicCreateBookingCommand
        {
            TenantId = tenant.Id,
            BookingTypeSlug = req.Slug,
            StartTime = req.StartTime,
            CustomerEmail = req.CustomerEmail,
            CustomerId = req.CustomerId
        }, ct);

        await Send.ResponseAsync(result, 201, ct);
    }
}
