using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Commands.Webhooks;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record DeleteWebhookCommand : IRequest
{
    public required string BookingTypeSlug { get; init; }
    public required Guid WebhookId { get; init; }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class DeleteWebhookHandler(
    ITenantContext tenantContext,
    IWebhookRepository webhookRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteWebhookCommand>
{
    public async Task Handle(DeleteWebhookCommand cmd, CancellationToken ct)
    {
        var webhook = await webhookRepo.GetByIdAsync(tenantContext.TenantId, cmd.WebhookId, ct)
            ?? throw new NotFoundException("Webhook", cmd.WebhookId);

        webhook.SoftDelete();
        await unitOfWork.SaveChangesAsync(ct);
    }
}
