using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.TenantPaymentConfig;

public sealed record CreateTenantPaymentConfigCommand : IRequest<TenantPaymentConfigDto>, IAuditable
{
    public required string ProviderName { get; init; }
    public required string Label { get; init; }
    public required string Settings { get; init; }
    public string? PublicNote { get; init; }
    public string? QrCodeUrl { get; init; }

    public Guid EntityId => Guid.Empty;
    public string EntityType => "TenantPaymentConfig";
    public string Action => "Create";
}

public sealed class CreateTenantPaymentConfigCommandValidator
    : AbstractValidator<CreateTenantPaymentConfigCommand>
{
    private static readonly string[] ValidProviders = ["PayMongo", "Maya", "Manual", "Stub"];

    public CreateTenantPaymentConfigCommandValidator()
    {
        RuleFor(x => x.ProviderName)
            .NotEmpty()
            .MaximumLength(50)
            .Must(p => ValidProviders.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"ProviderName must be one of: {string.Join(", ", ValidProviders)}");

        RuleFor(x => x.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Settings).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.PublicNote).MaximumLength(500).When(x => x.PublicNote is not null);
        RuleFor(x => x.QrCodeUrl).MaximumLength(2048).When(x => x.QrCodeUrl is not null);
    }
}

public sealed class CreateTenantPaymentConfigCommandHandler(
    ITenantPaymentConfigRepository repo,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateTenantPaymentConfigCommand, TenantPaymentConfigDto>
{
    public async Task<TenantPaymentConfigDto> Handle(
        CreateTenantPaymentConfigCommand cmd, CancellationToken ct)
    {
        var config = Domain.Models.TenantPaymentConfig.Create(
            tenantContext.TenantId,
            cmd.ProviderName,
            cmd.Label,
            cmd.Settings,
            cmd.PublicNote,
            cmd.QrCodeUrl);

        await repo.AddAsync(config, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return config.ToDto();
    }
}
