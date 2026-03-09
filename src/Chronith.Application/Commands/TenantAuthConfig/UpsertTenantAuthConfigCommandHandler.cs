using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Commands.TenantAuthConfig;

public sealed class UpsertTenantAuthConfigCommandHandler(
    ITenantAuthConfigRepository authConfigRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpsertTenantAuthConfigCommand, TenantAuthConfigDto>
{
    public async Task<TenantAuthConfigDto> Handle(
        UpsertTenantAuthConfigCommand request, CancellationToken cancellationToken)
    {
        var config = await authConfigRepository.GetByTenantIdAsync(
            tenantContext.TenantId, cancellationToken);

        if (config is null)
        {
            config = Domain.Models.TenantAuthConfig.Create(tenantContext.TenantId);
            await authConfigRepository.AddAsync(config, cancellationToken);
        }

        config.Update(request.AllowBuiltInAuth, request.OidcIssuer, request.OidcClientId,
            request.OidcAudience, request.MagicLinkEnabled);

        authConfigRepository.Update(config);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return config.ToDto();
    }
}
