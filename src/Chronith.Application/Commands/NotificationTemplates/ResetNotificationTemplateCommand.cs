using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Commands.NotificationTemplates;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record ResetNotificationTemplateCommand : IRequest<Unit>
{
    public required string EventType { get; init; }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ResetNotificationTemplateCommandHandler(
    ITenantContext tenantContext,
    INotificationTemplateRepository templateRepo,
    IDefaultTemplateSeeder templateSeeder)
    : IRequestHandler<ResetNotificationTemplateCommand, Unit>
{
    public async Task<Unit> Handle(ResetNotificationTemplateCommand cmd, CancellationToken ct)
    {
        await templateRepo.DeleteByEventTypeAsync(tenantContext.TenantId, cmd.EventType, ct);
        await templateSeeder.SeedForEventTypeAsync(tenantContext.TenantId, cmd.EventType, ct);

        return Unit.Value;
    }
}
