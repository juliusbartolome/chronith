using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntryEntity>
{
    public void Configure(EntityTypeBuilder<WaitlistEntryEntity> builder)
    {
        builder.ToTable("waitlist_entries", "chronith");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.CustomerId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(w => w.CustomerEmail)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(w => w.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(w => new { w.TenantId, w.BookingTypeId, w.Status });
        builder.HasIndex(w => new { w.TenantId, w.IsDeleted });
    }
}
