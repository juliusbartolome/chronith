using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntryEntity>
{
    public void Configure(EntityTypeBuilder<AuditEntryEntity> builder)
    {
        builder.ToTable("audit_entries", "chronith");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.UserRole)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(a => a.OldValues)
            .HasColumnType("jsonb");

        builder.Property(a => a.NewValues)
            .HasColumnType("jsonb");

        builder.Property(a => a.Metadata)
            .HasColumnType("jsonb");

        // Composite index: list by tenant + time range
        builder.HasIndex(a => new { a.TenantId, a.Timestamp });

        // Composite index: list by tenant + entity (history of a specific entity)
        builder.HasIndex(a => new { a.TenantId, a.EntityType, a.EntityId, a.Timestamp })
            .IsDescending(false, false, false, true);

        // Composite index: list by tenant + user (activity of a specific user)
        builder.HasIndex(a => new { a.TenantId, a.UserId, a.Timestamp })
            .IsDescending(false, false, true);
    }
}
