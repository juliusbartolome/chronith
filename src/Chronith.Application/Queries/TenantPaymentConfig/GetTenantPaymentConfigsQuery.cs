using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.TenantPaymentConfig;

public sealed record GetTenantPaymentConfigsQuery
    : IRequest<IReadOnlyList<TenantPaymentConfigDto>>, IQuery;

public sealed class GetTenantPaymentConfigsQueryHandler(
    ITenantPaymentConfigRepository repo,
    ITenantContext tenantContext)
    : IRequestHandler<GetTenantPaymentConfigsQuery, IReadOnlyList<TenantPaymentConfigDto>>
{
    public async Task<IReadOnlyList<TenantPaymentConfigDto>> Handle(
        GetTenantPaymentConfigsQuery query, CancellationToken ct)
    {
        var configs = await repo.ListByTenantAsync(tenantContext.TenantId, ct);
        return configs.Select(c => c.ToDto()).ToList();
    }
}
