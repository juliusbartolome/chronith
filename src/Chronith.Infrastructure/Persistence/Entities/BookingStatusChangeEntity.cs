using Chronith.Domain.Enums;

namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class BookingStatusChangeEntity
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public BookingStatus FromStatus { get; set; }
    public BookingStatus ToStatus { get; set; }
    public string ChangedById { get; set; } = string.Empty;
    public string ChangedByRole { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; }

    // Navigation
    public BookingEntity? Booking { get; set; }
}
