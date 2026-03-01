using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class BookingStatusChangeConfiguration : IEntityTypeConfiguration<BookingStatusChangeEntity>
{
    public void Configure(EntityTypeBuilder<BookingStatusChangeEntity> builder)
    {
        builder.ToTable("booking_status_changes", "chronith");

        builder.HasKey(sc => sc.Id);

        builder.Property(sc => sc.ChangedById)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(sc => sc.ChangedByRole)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(sc => sc.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(sc => sc.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasIndex(sc => sc.BookingId);
    }
}
