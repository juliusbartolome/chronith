using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class BookingConfiguration : IEntityTypeConfiguration<BookingEntity>
{
    public void Configure(EntityTypeBuilder<BookingEntity> builder)
    {
        builder.ToTable("bookings", "chronith");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.CustomerId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.CustomerEmail)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.PaymentReference)
            .HasMaxLength(200);

        builder.Property(b => b.CheckoutUrl)
            .HasColumnName("checkout_url")
            .HasMaxLength(2048);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(b => b.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // Core conflict detection index
        builder.HasIndex(b => new { b.TenantId, b.BookingTypeId, b.Start, b.End });
        builder.HasIndex(b => new { b.BookingTypeId, b.Status, b.Start, b.End });
        builder.HasIndex(b => new { b.TenantId, b.IsDeleted });
        builder.HasIndex(b => b.CustomerId);

        builder.HasMany(b => b.StatusChanges)
            .WithOne(sc => sc.Booking)
            .HasForeignKey(sc => sc.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
