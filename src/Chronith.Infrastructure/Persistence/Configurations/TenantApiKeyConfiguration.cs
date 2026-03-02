using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class TenantApiKeyConfiguration : IEntityTypeConfiguration<TenantApiKeyEntity>
{
    public void Configure(EntityTypeBuilder<TenantApiKeyEntity> builder)
    {
        builder.ToTable("TenantApiKeys", "chronith");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Role).HasMaxLength(50).IsRequired();

        // Primary auth lookup — unique hash
        builder.HasIndex(e => e.KeyHash)
            .IsUnique()
            .HasDatabaseName("IX_TenantApiKeys_KeyHash");

        // List endpoint filter
        builder.HasIndex(e => new { e.TenantId, e.IsRevoked })
            .HasDatabaseName("IX_TenantApiKeys_TenantId_IsRevoked");
    }
}
