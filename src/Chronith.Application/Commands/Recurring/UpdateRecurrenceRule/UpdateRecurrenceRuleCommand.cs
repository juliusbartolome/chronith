using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Recurring.UpdateRecurrenceRule;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record UpdateRecurrenceRuleCommand : IRequest<RecurrenceRuleDto>
{
    public required Guid Id { get; init; }
    public required RecurrenceFrequency Frequency { get; init; }
    public required int Interval { get; init; }
    public IReadOnlyList<DayOfWeek>? DaysOfWeek { get; init; }
    public required DateOnly SeriesStart { get; init; }
    public DateOnly? SeriesEnd { get; init; }
    public int? MaxOccurrences { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class UpdateRecurrenceRuleValidator : AbstractValidator<UpdateRecurrenceRuleCommand>
{
    public UpdateRecurrenceRuleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Interval).GreaterThanOrEqualTo(1);
        RuleFor(x => x.SeriesEnd)
            .Must((cmd, end) => !end.HasValue || end.Value >= cmd.SeriesStart)
            .WithMessage("SeriesEnd cannot be before SeriesStart.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class UpdateRecurrenceRuleHandler(
    IRecurrenceRuleRepository recurrenceRuleRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateRecurrenceRuleCommand, RecurrenceRuleDto>
{
    public async Task<RecurrenceRuleDto> Handle(UpdateRecurrenceRuleCommand cmd, CancellationToken ct)
    {
        var rule = await recurrenceRuleRepo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException("RecurrenceRule", cmd.Id);

        rule.Update(
            cmd.Frequency,
            cmd.Interval,
            cmd.DaysOfWeek,
            cmd.SeriesStart,
            cmd.SeriesEnd,
            cmd.MaxOccurrences);

        recurrenceRuleRepo.Update(rule);
        await unitOfWork.SaveChangesAsync(ct);

        return rule.ToDto();
    }
}
