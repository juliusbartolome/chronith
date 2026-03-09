using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class RecurrenceRuleMapper
{
    public static RecurrenceRuleDto ToDto(this RecurrenceRule rule) =>
        new(rule.Id,
            rule.TenantId,
            rule.BookingTypeId,
            rule.Frequency,
            rule.Interval,
            rule.DaysOfWeek,
            rule.SeriesStart,
            rule.SeriesEnd,
            rule.MaxOccurrences,
            rule.CreatedAt);
}
