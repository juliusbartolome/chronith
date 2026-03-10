using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Recurring.CancelRecurrenceRule;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CancelRecurrenceRuleCommand : IRequest, IAuditable
{
    public required Guid Id { get; init; }

    public Guid EntityId => Id;
    public string EntityType => "RecurrenceRule";
    public string Action => "Cancel";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CancelRecurrenceRuleValidator : AbstractValidator<CancelRecurrenceRuleCommand>
{
    public CancelRecurrenceRuleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CancelRecurrenceRuleHandler(
    IRecurrenceRuleRepository recurrenceRuleRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CancelRecurrenceRuleCommand>
{
    public async Task Handle(CancelRecurrenceRuleCommand cmd, CancellationToken ct)
    {
        var rule = await recurrenceRuleRepo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException("RecurrenceRule", cmd.Id);

        rule.SoftDelete();
        recurrenceRuleRepo.Update(rule);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
