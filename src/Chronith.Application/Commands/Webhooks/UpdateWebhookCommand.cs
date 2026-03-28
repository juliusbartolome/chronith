using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Webhooks;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record UpdateWebhookCommand : IRequest<WebhookDto>
{
    public required string BookingTypeSlug { get; init; }
    public required Guid WebhookId { get; init; }
    public string? Url { get; init; }
    public string? Secret { get; init; }
    public IReadOnlyList<string>? EventTypes { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class UpdateWebhookCommandValidator : AbstractValidator<UpdateWebhookCommand>
{
    public UpdateWebhookCommandValidator()
    {
        RuleFor(x => x.BookingTypeSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.WebhookId).NotEmpty();
        RuleFor(x => x.Url).Must(u => Uri.TryCreate(u, UriKind.Absolute, out _))
            .WithMessage("Url must be a valid absolute URI.").MaximumLength(2048)
            .When(x => x.Url is not null);
        RuleFor(x => x.Secret).MinimumLength(16)
            .When(x => x.Secret is not null);
        RuleFor(x => x.EventTypes).NotEmpty()
            .WithMessage("EventTypes cannot be empty when provided.")
            .When(x => x.EventTypes is not null);
        RuleForEach(x => x.EventTypes).Must(WebhookEventTypes.IsValid)
            .WithMessage("'{PropertyValue}' is not a valid webhook event type.")
            .When(x => x.EventTypes is not null);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class UpdateWebhookCommandHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository bookingTypeRepo,
    IWebhookRepository webhookRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateWebhookCommand, WebhookDto>
{
    public async Task<WebhookDto> Handle(UpdateWebhookCommand cmd, CancellationToken ct)
    {
        _ = await bookingTypeRepo.GetBySlugAsync(tenantContext.TenantId, cmd.BookingTypeSlug, ct)
            ?? throw new NotFoundException("BookingType", cmd.BookingTypeSlug);

        var webhook = await webhookRepo.GetByIdAsync(tenantContext.TenantId, cmd.WebhookId, ct)
            ?? throw new NotFoundException("Webhook", cmd.WebhookId);

        webhook.Update(cmd.Url, cmd.Secret, cmd.EventTypes);
        await webhookRepo.UpdateAsync(webhook, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return webhook.ToDto();
    }
}
