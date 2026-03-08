using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class BookingReminderConfiguration : IEntityTypeConfiguration<BookingReminderEntity>
{
    public void Configure(EntityTypeBuilder<BookingReminderEntity> builder)
    {
        builder.ToTable("booking_reminders", "chronith");

        builder.HasKey(r => r.Id);

        // Prevent duplicate reminders for the same booking + interval
        builder.HasIndex(r => new { r.BookingId, r.IntervalMinutes })
            .IsUnique()
            .HasDatabaseName("IX_booking_reminders_BookingId_IntervalMinutes");

        builder.HasIndex(r => r.BookingId)
            .HasDatabaseName("IX_booking_reminders_BookingId");
    }
}
