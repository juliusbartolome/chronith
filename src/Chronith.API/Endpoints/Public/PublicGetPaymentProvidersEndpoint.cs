using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.TenantPaymentConfig;
using Chronith.Domain.Exceptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public;

public sealed class PublicGetPaymentProvidersEndpoint(ISender sender, ITenantRepository tenantRepo)
    : EndpointWithoutRequest<IReadOnlyList<PaymentProviderSummaryDto>>
{
    public override void Configure()
    {
        Get("/public/{tenantSlug}/payment-providers");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Public"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("tenantSlug")!;
        var tenant = await tenantRepo.GetBySlugAsync(slug, ct)
            ?? throw new NotFoundException("Tenant", slug);

        var result = await sender.Send(new GetPublicPaymentProvidersQuery(tenant.Id), ct);
        await Send.OkAsync(result, ct);
    }
}
