namespace Chronith.Domain.Models;

public sealed class BookingReminder
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid BookingId { get; init; }
    public int IntervalMinutes { get; init; }
    public DateTimeOffset SentAt { get; init; }
}
