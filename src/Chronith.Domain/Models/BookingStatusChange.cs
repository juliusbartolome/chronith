namespace Chronith.Domain.Models;
using Chronith.Domain.Enums;

public sealed class BookingStatusChange
{
    public Guid Id { get; }
    public Guid BookingId { get; }
    public BookingStatus FromStatus { get; }
    public BookingStatus ToStatus { get; }
    public string ChangedById { get; }
    public string ChangedByRole { get; }
    public DateTimeOffset ChangedAt { get; }

    public BookingStatusChange(
        Guid bookingId,
        BookingStatus from,
        BookingStatus to,
        string changedById,
        string changedByRole)
    {
        Id = Guid.NewGuid();
        BookingId = bookingId;
        FromStatus = from;
        ToStatus = to;
        ChangedById = changedById;
        ChangedByRole = changedByRole;
        ChangedAt = DateTimeOffset.UtcNow;
    }
}
