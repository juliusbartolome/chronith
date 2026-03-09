using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

internal static class RecurrenceRuleEntityMapper
{
    public static RecurrenceRuleEntity ToEntity(this RecurrenceRule r) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId,
        BookingTypeId = r.BookingTypeId,
        Frequency = r.Frequency.ToString(),
        Interval = r.Interval,
        DaysOfWeek = r.DaysOfWeek?.Select(d => (int)d).ToArray(),
        SeriesStart = r.SeriesStart,
        SeriesEnd = r.SeriesEnd,
        MaxOccurrences = r.MaxOccurrences,
        IsDeleted = r.IsDeleted,
        CreatedAt = r.CreatedAt
    };

    public static RecurrenceRule ToDomain(this RecurrenceRuleEntity e) =>
        RecurrenceRule.Hydrate(
            e.Id,
            e.TenantId,
            e.BookingTypeId,
            Enum.Parse<RecurrenceFrequency>(e.Frequency),
            e.Interval,
            e.DaysOfWeek?.Select(d => (DayOfWeek)d).ToList().AsReadOnly(),
            e.SeriesStart,
            e.SeriesEnd,
            e.MaxOccurrences,
            e.IsDeleted,
            e.CreatedAt);
}
