using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

/// <summary>
/// Generates available (unbooked) slots for a given booking type and date range.
/// Implemented purely in C# — no DB calls.
/// </summary>
public interface ISlotGeneratorService
{
    IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> GenerateAvailableSlots(
        BookingType bookingType,
        TenantTimeZone tz,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> bookedSlots);
}
