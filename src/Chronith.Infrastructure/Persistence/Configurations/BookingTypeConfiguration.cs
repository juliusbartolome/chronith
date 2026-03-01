using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class BookingTypeConfiguration : IEntityTypeConfiguration<BookingTypeEntity>
{
    public void Configure(EntityTypeBuilder<BookingTypeEntity> builder)
    {
        builder.ToTable("booking_types", "chronith");

        builder.HasKey(bt => bt.Id);

        builder.Property(bt => bt.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(bt => bt.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(bt => bt.PaymentProvider)
            .HasMaxLength(100);

        builder.Property(bt => bt.Kind)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(bt => bt.PaymentMode)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(bt => bt.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // Unique slug per tenant
        builder.HasIndex(bt => new { bt.TenantId, bt.Slug })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasIndex(bt => new { bt.TenantId, bt.IsDeleted });

        builder.HasMany(bt => bt.AvailabilityWindows)
            .WithOne(w => w.BookingType)
            .HasForeignKey(w => w.BookingTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(bt => bt.Bookings)
            .WithOne(b => b.BookingType)
            .HasForeignKey(b => b.BookingTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(bt => bt.Webhooks)
            .WithOne(w => w.BookingType)
            .HasForeignKey(w => w.BookingTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
