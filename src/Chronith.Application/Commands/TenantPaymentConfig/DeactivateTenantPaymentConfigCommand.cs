using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Commands.TenantPaymentConfig;

public sealed record DeactivateTenantPaymentConfigCommand(Guid Id) : IRequest, IAuditable
{
    public Guid EntityId => Id;
    public string EntityType => "TenantPaymentConfig";
    public string Action => "Deactivate";
}

public sealed class DeactivateTenantPaymentConfigCommandHandler(
    ITenantPaymentConfigRepository repo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeactivateTenantPaymentConfigCommand>
{
    public async Task Handle(DeactivateTenantPaymentConfigCommand cmd, CancellationToken ct)
    {
        var config = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException("TenantPaymentConfig", cmd.Id);

        config.Deactivate();
        await repo.UpdateAsync(config, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
