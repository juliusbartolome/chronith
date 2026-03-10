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
    INotificationTemplateRepository templateRepo)
    : IRequestHandler<ResetNotificationTemplateCommand, Unit>
{
    public async Task<Unit> Handle(ResetNotificationTemplateCommand cmd, CancellationToken ct)
    {
        await templateRepo.DeleteByEventTypeAsync(tenantContext.TenantId, cmd.EventType, ct);

        // IDefaultTemplateSeeder will be wired up in Task 11
        // For now, deletion is sufficient to reset the templates

        return Unit.Value;
    }
}
