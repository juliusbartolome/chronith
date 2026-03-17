using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class BookingReminderEntityMapper
{
    public static BookingReminder ToDomain(BookingReminderEntity entity)
        => new()
        {
            Id = entity.Id,
            BookingId = entity.BookingId,
            IntervalMinutes = entity.IntervalMinutes,
            SentAt = entity.SentAt
        };

    public static BookingReminderEntity ToEntity(BookingReminder domain)
        => new()
        {
            Id = domain.Id,
            BookingId = domain.BookingId,
            IntervalMinutes = domain.IntervalMinutes,
            SentAt = domain.SentAt
        };
}
