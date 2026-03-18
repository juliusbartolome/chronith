using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.TenantPaymentConfig;

public sealed record UpdateTenantPaymentConfigCommand : IRequest<TenantPaymentConfigDto>, IAuditable
{
    public required Guid Id { get; init; }
    public required string Label { get; init; }
    public required string Settings { get; init; }
    public string? PublicNote { get; init; }
    public string? QrCodeUrl { get; init; }

    public Guid EntityId => Id;
    public string EntityType => "TenantPaymentConfig";
    public string Action => "Update";
}

public sealed class UpdateTenantPaymentConfigCommandValidator
    : AbstractValidator<UpdateTenantPaymentConfigCommand>
{
    public UpdateTenantPaymentConfigCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Settings).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.PublicNote).MaximumLength(500).When(x => x.PublicNote is not null);
        RuleFor(x => x.QrCodeUrl).MaximumLength(2048).When(x => x.QrCodeUrl is not null);
    }
}

public sealed class UpdateTenantPaymentConfigCommandHandler(
    ITenantPaymentConfigRepository repo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateTenantPaymentConfigCommand, TenantPaymentConfigDto>
{
    public async Task<TenantPaymentConfigDto> Handle(
        UpdateTenantPaymentConfigCommand cmd, CancellationToken ct)
    {
        var config = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException("TenantPaymentConfig", cmd.Id);

        config.UpdateDetails(cmd.Label, cmd.Settings, cmd.PublicNote, cmd.QrCodeUrl);
        await repo.UpdateAsync(config, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return config.ToDto();
    }
}
