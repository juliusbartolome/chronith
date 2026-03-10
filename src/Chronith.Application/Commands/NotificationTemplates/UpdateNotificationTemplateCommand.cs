using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.NotificationTemplates;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record UpdateNotificationTemplateCommand : IRequest<NotificationTemplateDto>
{
    public required Guid Id { get; init; }
    public string? Subject { get; init; }
    public required string Body { get; init; }
    public required bool IsActive { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class UpdateNotificationTemplateCommandValidator
    : AbstractValidator<UpdateNotificationTemplateCommand>
{
    public UpdateNotificationTemplateCommandValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10000);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class UpdateNotificationTemplateCommandHandler(
    ITenantContext tenantContext,
    INotificationTemplateRepository templateRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateNotificationTemplateCommand, NotificationTemplateDto>
{
    public async Task<NotificationTemplateDto> Handle(
        UpdateNotificationTemplateCommand cmd, CancellationToken ct)
    {
        var template = await templateRepo.GetByIdAsync(tenantContext.TenantId, cmd.Id, ct)
            ?? throw new NotFoundException("NotificationTemplate", cmd.Id);

        template.UpdateBody(cmd.Subject, cmd.Body);

        if (cmd.IsActive)
            template.Activate();
        else
            template.Deactivate();

        await templateRepo.UpdateAsync(template, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return template.ToDto();
    }
}
