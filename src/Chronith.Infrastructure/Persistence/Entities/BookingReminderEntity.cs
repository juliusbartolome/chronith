namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class BookingReminderEntity
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public int IntervalMinutes { get; set; }
    public DateTimeOffset SentAt { get; set; }
}
