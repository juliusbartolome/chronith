using Chronith.Domain.Enums;

namespace Chronith.Application.DTOs;

public sealed record RecurrenceRuleDto(
    Guid Id,
    Guid TenantId,
    Guid BookingTypeId,
    Guid CustomerId,
    Guid? StaffMemberId,
    RecurrenceFrequency Frequency,
    int Interval,
    IReadOnlyList<DayOfWeek>? DaysOfWeek,
    TimeOnly StartTime,
    TimeSpan Duration,
    DateOnly SeriesStart,
    DateOnly? SeriesEnd,
    int? MaxOccurrences,
    bool IsActive,
    DateTimeOffset CreatedAt);
