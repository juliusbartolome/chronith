using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Commands.TenantPaymentConfig;

public sealed record DeleteTenantPaymentConfigCommand(Guid Id) : IRequest, IAuditable
{
    public Guid EntityId => Id;
    public string EntityType => "TenantPaymentConfig";
    public string Action => "Delete";
}

public sealed class DeleteTenantPaymentConfigCommandHandler(
    ITenantPaymentConfigRepository repo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteTenantPaymentConfigCommand>
{
    public async Task Handle(DeleteTenantPaymentConfigCommand cmd, CancellationToken ct)
    {
        _ = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException("TenantPaymentConfig", cmd.Id);

        await repo.SoftDeleteAsync(cmd.Id, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
