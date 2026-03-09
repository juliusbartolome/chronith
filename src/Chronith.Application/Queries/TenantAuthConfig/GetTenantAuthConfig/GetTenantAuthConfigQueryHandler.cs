using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.TenantAuthConfig.GetTenantAuthConfig;

public sealed class GetTenantAuthConfigQueryHandler(
    ITenantAuthConfigRepository authConfigRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetTenantAuthConfigQuery, TenantAuthConfigDto?>
{
    public async Task<TenantAuthConfigDto?> Handle(
        GetTenantAuthConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await authConfigRepository.GetByTenantIdAsync(
            tenantContext.TenantId, cancellationToken);

        return config?.ToDto();
    }
}
