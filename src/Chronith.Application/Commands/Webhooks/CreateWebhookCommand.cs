using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Webhooks;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CreateWebhookCommand : IRequest<WebhookDto>
{
    public required string BookingTypeSlug { get; init; }
    public required string Url { get; init; }
    public required string Secret { get; init; }
    public required IReadOnlyList<string> EventTypes { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CreateWebhookValidator : AbstractValidator<CreateWebhookCommand>
{
    public CreateWebhookValidator()
    {
        RuleFor(x => x.BookingTypeSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Url).NotEmpty().Must(u => Uri.TryCreate(u, UriKind.Absolute, out _))
            .WithMessage("Url must be a valid absolute URI.").MaximumLength(2048);
        RuleFor(x => x.Secret).NotEmpty().MinimumLength(16);
        RuleFor(x => x.EventTypes).NotEmpty()
            .WithMessage("At least one event type is required.");
        RuleForEach(x => x.EventTypes).Must(WebhookEventTypes.IsValid)
            .WithMessage("'{PropertyValue}' is not a valid webhook event type.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreateWebhookHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository bookingTypeRepo,
    IWebhookRepository webhookRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateWebhookCommand, WebhookDto>
{
    public async Task<WebhookDto> Handle(CreateWebhookCommand cmd, CancellationToken ct)
    {
        var bt = await bookingTypeRepo.GetBySlugAsync(tenantContext.TenantId, cmd.BookingTypeSlug, ct)
            ?? throw new NotFoundException("BookingType", cmd.BookingTypeSlug);

        var webhook = Webhook.Create(tenantContext.TenantId, bt.Id, cmd.Url, cmd.Secret, cmd.EventTypes);
        await webhookRepo.AddAsync(webhook, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return webhook.ToDto();
    }
}
