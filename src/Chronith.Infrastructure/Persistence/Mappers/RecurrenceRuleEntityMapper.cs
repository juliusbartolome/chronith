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
        CustomerId = r.CustomerId,
        StaffMemberId = r.StaffMemberId,
        Frequency = r.Frequency.ToString(),
        Interval = r.Interval,
        DaysOfWeek = r.DaysOfWeek?.Select(d => (int)d).ToArray(),
        StartTime = r.StartTime,
        Duration = r.Duration,
        SeriesStart = r.SeriesStart,
        SeriesEnd = r.SeriesEnd,
        MaxOccurrences = r.MaxOccurrences,
        IsActive = r.IsActive,
        IsDeleted = r.IsDeleted,
        RowVersion = r.RowVersion,
        CreatedAt = r.CreatedAt
    };

    public static RecurrenceRule ToDomain(this RecurrenceRuleEntity e) =>
        RecurrenceRule.Hydrate(
            e.Id,
            e.TenantId,
            e.BookingTypeId,
            e.CustomerId,
            e.StaffMemberId,
            Enum.Parse<RecurrenceFrequency>(e.Frequency),
            e.Interval,
            e.DaysOfWeek?.Select(d => (DayOfWeek)d).ToList().AsReadOnly(),
            e.StartTime,
            e.Duration,
            e.SeriesStart,
            e.SeriesEnd,
            e.MaxOccurrences,
            e.IsActive,
            e.IsDeleted,
            e.RowVersion,
            e.CreatedAt);
}
