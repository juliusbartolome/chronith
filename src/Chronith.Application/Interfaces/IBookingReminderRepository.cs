using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IBookingReminderRepository
{
    /// <summary>Checks if a reminder has already been sent for this booking + interval.</summary>
    Task<bool> ExistsAsync(Guid bookingId, int intervalMinutes, CancellationToken ct = default);

    Task AddAsync(BookingReminder reminder, CancellationToken ct = default);

    Task<IReadOnlyList<BookingReminder>> ListByBookingAsync(
        Guid bookingId, CancellationToken ct = default);
}
