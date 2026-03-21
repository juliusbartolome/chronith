// src/Chronith.Application/Commands/ApiKeys/CreateApiKeyCommand.cs
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.ApiKeys;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CreateApiKeyCommand : IRequest<CreateApiKeyResult>, IAuditable
{
    public required string Description { get; init; }
    public required IReadOnlyList<string> Scopes { get; init; }

    // IAuditable — EntityId is Guid.Empty pre-creation
    public Guid EntityId => Guid.Empty;
    public string EntityType => "TenantApiKey";
    public string Action => "Create";
}

// ── Validator ────────────────────────────────────────────────────────────────

public sealed class CreateApiKeyValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Scopes)
            .NotEmpty()
            .WithMessage("At least one scope is required.");
        RuleForEach(x => x.Scopes)
            .Must(s => ApiKeyScope.All.Contains(s))
            .WithMessage((_, s) => $"'{s}' is not a valid API key scope.");
    }
}

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class CreateApiKeyHandler(
    ITenantContext tenantContext,
    IApiKeyRepository apiKeyRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateApiKeyCommand, CreateApiKeyResult>
{
    public async Task<CreateApiKeyResult> Handle(CreateApiKeyCommand cmd, CancellationToken ct)
    {
        var (rawKey, keyHash) = TenantApiKey.GenerateKey();

        var key = new TenantApiKey
        {
            TenantId = tenantContext.TenantId,
            KeyHash = keyHash,
            Description = cmd.Description,
            Scopes = cmd.Scopes,
        };

        await apiKeyRepo.AddAsync(key, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new CreateApiKeyResult(
            Id: key.Id,
            RawKey: rawKey,
            Description: key.Description,
            Scopes: key.Scopes,
            CreatedAt: key.CreatedAt);
    }
}
