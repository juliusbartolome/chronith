using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class TenantApiKeyConfiguration : IEntityTypeConfiguration<TenantApiKeyEntity>
{
    public void Configure(EntityTypeBuilder<TenantApiKeyEntity> builder)
    {
        builder.ToTable("tenant_api_keys", "chronith");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Scopes)
            .HasColumnType("text[]")
            .HasColumnName("scopes")
            .IsRequired();
        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired(false);

        // Primary auth lookup — unique hash
        builder.HasIndex(e => e.KeyHash)
            .IsUnique()
            .HasDatabaseName("IX_tenant_api_keys_key_hash");

        // List endpoint filter
        builder.HasIndex(e => new { e.TenantId, e.IsRevoked })
            .HasDatabaseName("IX_tenant_api_keys_tenant_id_is_revoked");
    }
}
