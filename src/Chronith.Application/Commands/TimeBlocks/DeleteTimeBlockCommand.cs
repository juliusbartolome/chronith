using Chronith.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.TimeBlocks;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record DeleteTimeBlockCommand : IRequest, IAuditable
{
    public required Guid TimeBlockId { get; init; }

    public Guid EntityId => TimeBlockId;
    public string EntityType => "TimeBlock";
    public string Action => "Delete";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class DeleteTimeBlockValidator : AbstractValidator<DeleteTimeBlockCommand>
{
    public DeleteTimeBlockValidator()
    {
        RuleFor(x => x.TimeBlockId).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class DeleteTimeBlockHandler(
    ITenantContext tenantContext,
    ITimeBlockRepository timeBlockRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteTimeBlockCommand>
{
    public async Task Handle(DeleteTimeBlockCommand cmd, CancellationToken ct)
    {
        await timeBlockRepo.DeleteAsync(tenantContext.TenantId, cmd.TimeBlockId, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
