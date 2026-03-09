using Chronith.Domain.Enums;

namespace Chronith.Application.DTOs;

public sealed record RecurrenceRuleDto(
    Guid Id,
    Guid TenantId,
    Guid BookingTypeId,
    RecurrenceFrequency Frequency,
    int Interval,
    IReadOnlyList<DayOfWeek>? DaysOfWeek,
    DateOnly SeriesStart,
    DateOnly? SeriesEnd,
    int? MaxOccurrences,
    DateTimeOffset CreatedAt);
