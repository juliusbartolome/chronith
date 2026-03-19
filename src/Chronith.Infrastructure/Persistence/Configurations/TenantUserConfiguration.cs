using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class TenantUserConfiguration : IEntityTypeConfiguration<TenantUserEntity>
{
    public void Configure(EntityTypeBuilder<TenantUserEntity> builder)
    {
        builder.ToTable("TenantUsers");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.EmailEncrypted);
        builder.Property(u => u.EmailToken).HasMaxLength(64);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).IsRequired()
            .HasConversion<string>(); // store as string e.g. "Owner"
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.IsEmailVerified).IsRequired();
        // Unique email per tenant
        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
        // Blind index for encrypted email lookup
        builder.HasIndex(u => new { u.TenantId, u.EmailToken })
            .HasFilter("\"EmailToken\" IS NOT NULL")
            .HasDatabaseName("ix_tenantusers_email_token");
    }
}
