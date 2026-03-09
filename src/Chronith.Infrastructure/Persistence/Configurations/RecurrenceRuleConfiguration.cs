using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class RecurrenceRuleConfiguration : IEntityTypeConfiguration<RecurrenceRuleEntity>
{
    public void Configure(EntityTypeBuilder<RecurrenceRuleEntity> builder)
    {
        builder.ToTable("recurrence_rules", "chronith");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Frequency)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.DaysOfWeek)
            .HasColumnType("jsonb");

        builder.Property(r => r.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(r => new { r.TenantId, r.IsDeleted });

        builder.HasOne<BookingTypeEntity>()
            .WithMany()
            .HasForeignKey(r => r.BookingTypeId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
