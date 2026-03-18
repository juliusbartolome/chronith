using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Commands.TenantPaymentConfig;

public sealed record ActivateTenantPaymentConfigCommand(Guid Id) : IRequest, IAuditable
{
    public Guid EntityId => Id;
    public string EntityType => "TenantPaymentConfig";
    public string Action => "Activate";
}

public sealed class ActivateTenantPaymentConfigCommandHandler(
    ITenantPaymentConfigRepository repo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ActivateTenantPaymentConfigCommand>
{
    public async Task Handle(ActivateTenantPaymentConfigCommand cmd, CancellationToken ct)
    {
        var config = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException("TenantPaymentConfig", cmd.Id);

        // For API types, ensure only one active at a time
        if (!config.ProviderName.Equals("Manual", StringComparison.OrdinalIgnoreCase))
            await repo.DeactivateAllByProviderNameAsync(config.TenantId, config.ProviderName, ct);

        config.Activate();
        await repo.UpdateAsync(config, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
