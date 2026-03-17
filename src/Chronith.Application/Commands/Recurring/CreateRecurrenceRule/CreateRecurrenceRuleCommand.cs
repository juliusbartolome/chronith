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

public sealed record CreateRecurrenceRuleCommand : IRequest<RecurrenceRuleDto>, IAuditable
{
    public required string BookingTypeSlug { get; init; }
    public required Guid CustomerId { get; init; }
    public Guid? StaffMemberId { get; init; }
    public required RecurrenceFrequency Frequency { get; init; }
    public required int Interval { get; init; }
    public IReadOnlyList<DayOfWeek>? DaysOfWeek { get; init; }
    public required TimeOnly StartTime { get; init; }
    public required TimeSpan Duration { get; init; }
    public required DateOnly SeriesStart { get; init; }
    public DateOnly? SeriesEnd { get; init; }
    public int? MaxOccurrences { get; init; }

    // IAuditable — EntityId is Guid.Empty pre-creation
    public Guid EntityId => Guid.Empty;
    public string EntityType => "RecurrenceRule";
    public string Action => "Create";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CreateRecurrenceRuleValidator : AbstractValidator<CreateRecurrenceRuleCommand>
{
    public CreateRecurrenceRuleValidator()
    {
        RuleFor(x => x.BookingTypeSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.Interval).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Duration).GreaterThan(TimeSpan.Zero);
        RuleFor(x => x.MaxOccurrences)
            .GreaterThanOrEqualTo(1)
            .When(x => x.MaxOccurrences.HasValue);
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
            cmd.CustomerId,
            cmd.StaffMemberId,
            cmd.Frequency,
            cmd.Interval,
            cmd.DaysOfWeek,
            cmd.StartTime,
            cmd.Duration,
            cmd.SeriesStart,
            cmd.SeriesEnd,
            cmd.MaxOccurrences);

        await recurrenceRuleRepo.AddAsync(rule, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return rule.ToDto();
    }
}
