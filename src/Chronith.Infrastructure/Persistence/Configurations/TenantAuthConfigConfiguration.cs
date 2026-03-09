using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class TenantAuthConfigConfiguration : IEntityTypeConfiguration<TenantAuthConfigEntity>
{
    public void Configure(EntityTypeBuilder<TenantAuthConfigEntity> builder)
    {
        builder.ToTable("tenant_auth_configs", "chronith");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.OidcIssuer)
            .HasMaxLength(500);

        builder.Property(c => c.OidcClientId)
            .HasMaxLength(200);

        builder.Property(c => c.OidcAudience)
            .HasMaxLength(200);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired();

        builder.HasIndex(c => c.TenantId)
            .IsUnique();
    }
}
