using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class TenantUserRefreshTokenConfiguration : IEntityTypeConfiguration<TenantUserRefreshTokenEntity>
{
    public void Configure(EntityTypeBuilder<TenantUserRefreshTokenEntity> builder)
    {
        builder.ToTable("TenantUserRefreshTokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(64); // SHA-256 hex = 64 chars
        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.HasOne(t => t.TenantUser)
            .WithMany()
            .HasForeignKey(t => t.TenantUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => t.TokenHash); // for fast lookup on refresh
    }
}
