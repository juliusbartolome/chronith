using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Queries.NotificationTemplates;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record PreviewNotificationTemplateQuery(Guid Id, Dictionary<string, string> Variables)
    : IRequest<NotificationTemplatePreviewDto>, IQuery;

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class PreviewNotificationTemplateQueryValidator
    : AbstractValidator<PreviewNotificationTemplateQuery>
{
    public PreviewNotificationTemplateQueryValidator()
    {
        RuleFor(x => x.Variables)
            .Must(v => v.Count <= 50)
            .WithMessage("Variables dictionary must not exceed 50 keys.");

        RuleForEach(x => x.Variables)
            .Must(kv => kv.Key.Length <= 100)
            .WithMessage("Each variable key must not exceed 100 characters.");

        RuleForEach(x => x.Variables)
            .Must(kv => kv.Value.Length <= 5000)
            .WithMessage("Each variable value must not exceed 5000 characters.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class PreviewNotificationTemplateQueryHandler(
    ITenantContext tenantContext,
    INotificationTemplateRepository templateRepo)
    : IRequestHandler<PreviewNotificationTemplateQuery, NotificationTemplatePreviewDto>
{
    public async Task<NotificationTemplatePreviewDto> Handle(
        PreviewNotificationTemplateQuery query, CancellationToken ct)
    {
        var template = await templateRepo.GetByIdAsync(tenantContext.TenantId, query.Id, ct)
            ?? throw new NotFoundException("NotificationTemplate", query.Id);

        var body = Substitute(template.Body, query.Variables);
        var subject = template.Subject is not null
            ? Substitute(template.Subject, query.Variables)
            : null;

        return new NotificationTemplatePreviewDto(subject, body);
    }

    private static string Substitute(string text, Dictionary<string, string> variables)
    {
        foreach (var (key, value) in variables)
            text = text.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        return text;
    }
}
