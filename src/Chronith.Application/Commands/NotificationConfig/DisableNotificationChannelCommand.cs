using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.NotificationConfig;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record DisableNotificationChannelCommand : IRequest, IAuditable
{
    public required string ChannelType { get; init; }

    public Guid EntityId => Guid.Empty;
    public string EntityType => "TenantNotificationConfig";
    public string Action => "DisableChannel";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class DisableNotificationChannelValidator : AbstractValidator<DisableNotificationChannelCommand>
{
    public DisableNotificationChannelValidator()
    {
        RuleFor(x => x.ChannelType).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class DisableNotificationChannelHandler(
    ITenantContext tenantContext,
    INotificationConfigRepository configRepo)
    : IRequestHandler<DisableNotificationChannelCommand>
{
    public async Task Handle(DisableNotificationChannelCommand cmd, CancellationToken ct)
    {
        var config = await configRepo.GetByChannelTypeAsync(
            tenantContext.TenantId, cmd.ChannelType, ct)
            ?? throw new NotFoundException("NotificationConfig", cmd.ChannelType);

        config.Disable();
        await configRepo.UpdateAsync(config, ct);
    }
}
