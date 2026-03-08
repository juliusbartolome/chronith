using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class BookingReminderRepository(ChronithDbContext db)
    : IBookingReminderRepository
{
    public async Task<bool> ExistsAsync(
        Guid bookingId, int intervalMinutes, CancellationToken ct = default)
    {
        return await db.BookingReminders
            .AsNoTracking()
            .AnyAsync(r => r.BookingId == bookingId && r.IntervalMinutes == intervalMinutes, ct);
    }

    public async Task AddAsync(BookingReminder reminder, CancellationToken ct = default)
    {
        var entity = BookingReminderEntityMapper.ToEntity(reminder);
        await db.BookingReminders.AddAsync(entity, ct);
    }

    public async Task<IReadOnlyList<BookingReminder>> ListByBookingAsync(
        Guid bookingId, CancellationToken ct = default)
    {
        var entities = await db.BookingReminders
            .AsNoTracking()
            .Where(r => r.BookingId == bookingId)
            .OrderBy(r => r.IntervalMinutes)
            .ToListAsync(ct);

        return entities.Select(BookingReminderEntityMapper.ToDomain).ToList();
    }
}
