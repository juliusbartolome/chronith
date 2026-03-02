using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.ApiKeys;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record RevokeApiKeyCommand(Guid Id) : IRequest;

// ── Validator ────────────────────────────────────────────────────────────────

public sealed class RevokeApiKeyValidator : AbstractValidator<RevokeApiKeyCommand>
{
    public RevokeApiKeyValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class RevokeApiKeyHandler(
    ITenantContext tenantContext,
    IApiKeyRepository apiKeyRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RevokeApiKeyCommand>
{
    public async Task Handle(RevokeApiKeyCommand cmd, CancellationToken ct)
    {
        var key = await apiKeyRepo.GetByIdAsync(cmd.Id, tenantContext.TenantId, ct)
            ?? throw new NotFoundException("ApiKey", cmd.Id);

        key.Revoke();
        await apiKeyRepo.UpdateAsync(key, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
