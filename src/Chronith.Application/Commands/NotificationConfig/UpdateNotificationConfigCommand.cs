using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.NotificationConfig;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record UpdateNotificationConfigCommand : IRequest<TenantNotificationConfigDto>
{
    public required string ChannelType { get; init; }
    public required string Settings { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class UpdateNotificationConfigValidator : AbstractValidator<UpdateNotificationConfigCommand>
{
    private static readonly string[] ValidChannelTypes = ["email", "sms", "push"];

    public UpdateNotificationConfigValidator()
    {
        RuleFor(x => x.ChannelType)
            .NotEmpty()
            .Must(ct => ValidChannelTypes.Contains(ct))
            .WithMessage("ChannelType must be one of: email, sms, push");
        RuleFor(x => x.Settings).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class UpdateNotificationConfigHandler(
    ITenantContext tenantContext,
    INotificationConfigRepository configRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateNotificationConfigCommand, TenantNotificationConfigDto>
{
    public async Task<TenantNotificationConfigDto> Handle(
        UpdateNotificationConfigCommand cmd, CancellationToken ct)
    {
        var existing = await configRepo.GetByChannelTypeAsync(
            tenantContext.TenantId, cmd.ChannelType, ct);

        if (existing is not null)
        {
            existing.UpdateSettings(cmd.Settings);
            existing.Enable();
            await configRepo.UpdateAsync(existing, ct);
            return existing.ToDto();
        }

        var config = TenantNotificationConfig.Create(
            tenantContext.TenantId, cmd.ChannelType, cmd.Settings);

        await configRepo.AddAsync(config, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return config.ToDto();
    }
}
