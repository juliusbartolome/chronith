using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.TenantPaymentConfig;

public sealed record GetPublicPaymentProvidersQuery(Guid TenantId)
    : IRequest<IReadOnlyList<PaymentProviderSummaryDto>>, IQuery;

public sealed class GetPublicPaymentProvidersQueryHandler(
    ITenantPaymentConfigRepository repo)
    : IRequestHandler<GetPublicPaymentProvidersQuery, IReadOnlyList<PaymentProviderSummaryDto>>
{
    public async Task<IReadOnlyList<PaymentProviderSummaryDto>> Handle(
        GetPublicPaymentProvidersQuery query, CancellationToken ct)
    {
        var configs = await repo.ListActiveByTenantAsync(query.TenantId, ct);
        return configs.Select(c => c.ToSummaryDto()).ToList();
    }
}
