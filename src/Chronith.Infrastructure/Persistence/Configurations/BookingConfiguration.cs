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
            .IsRequired();

        builder.Property(b => b.FirstName)
            .IsRequired()
            .HasMaxLength(200)
            .HasDefaultValue(string.Empty);

        builder.Property(b => b.LastName)
            .IsRequired()
            .HasMaxLength(200)
            .HasDefaultValue(string.Empty);

        builder.Property(b => b.Mobile)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(b => b.PaymentReference)
            .HasMaxLength(200);

        builder.Property(b => b.AmountInCentavos)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(b => b.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("PHP");

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

        // Composite indexes for query optimization
        builder.HasIndex(b => new { b.TenantId, b.BookingTypeId, b.Start, b.Status })
            .HasDatabaseName("ix_bookings_availability");

        builder.HasIndex(b => new { b.TenantId, b.CustomerId, b.Start })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_bookings_customer");

        builder.HasIndex(b => new { b.TenantId, b.StaffMemberId, b.Start })
            .HasDatabaseName("ix_bookings_staff");

        builder.HasIndex(b => new { b.RecurrenceRuleId, b.Start })
            .HasDatabaseName("ix_bookings_recurrence");

        builder.HasMany(b => b.StatusChanges)
            .WithOne(sc => sc.Booking)
            .HasForeignKey(sc => sc.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(b => b.StaffMemberId)
            .IsRequired(false);

        builder.HasIndex(b => b.StaffMemberId);

        builder.Property(b => b.CustomFields)
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.HasOne<CustomerEntity>()
            .WithMany()
            .HasForeignKey(b => b.CustomerAccountId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne<RecurrenceRuleEntity>()
            .WithMany()
            .HasForeignKey(b => b.RecurrenceRuleId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
