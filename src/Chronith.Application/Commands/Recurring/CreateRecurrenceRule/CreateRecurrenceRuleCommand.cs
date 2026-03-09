using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Recurring.CreateRecurrenceRule;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CreateRecurrenceRuleCommand : IRequest<RecurrenceRuleDto>
{
    public required string BookingTypeSlug { get; init; }
    public required RecurrenceFrequency Frequency { get; init; }
    public required int Interval { get; init; }
    public IReadOnlyList<DayOfWeek>? DaysOfWeek { get; init; }
    public required DateOnly SeriesStart { get; init; }
    public DateOnly? SeriesEnd { get; init; }
    public int? MaxOccurrences { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CreateRecurrenceRuleValidator : AbstractValidator<CreateRecurrenceRuleCommand>
{
    public CreateRecurrenceRuleValidator()
    {
        RuleFor(x => x.BookingTypeSlug).NotEmpty();
        RuleFor(x => x.Interval).GreaterThanOrEqualTo(1);
        RuleFor(x => x.SeriesEnd)
            .Must((cmd, end) => !end.HasValue || end.Value >= cmd.SeriesStart)
            .WithMessage("SeriesEnd cannot be before SeriesStart.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreateRecurrenceRuleHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository bookingTypeRepo,
    IRecurrenceRuleRepository recurrenceRuleRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateRecurrenceRuleCommand, RecurrenceRuleDto>
{
    public async Task<RecurrenceRuleDto> Handle(CreateRecurrenceRuleCommand cmd, CancellationToken ct)
    {
        var bookingType = await bookingTypeRepo.GetBySlugAsync(tenantContext.TenantId, cmd.BookingTypeSlug, ct)
            ?? throw new NotFoundException("BookingType", cmd.BookingTypeSlug);

        var rule = RecurrenceRule.Create(
            tenantContext.TenantId,
            bookingType.Id,
            cmd.Frequency,
            cmd.Interval,
            cmd.DaysOfWeek,
            cmd.SeriesStart,
            cmd.SeriesEnd,
            cmd.MaxOccurrences);

        await recurrenceRuleRepo.AddAsync(rule, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return rule.ToDto();
    }
}
