using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettingsEntity>
{
    public void Configure(EntityTypeBuilder<TenantSettingsEntity> builder)
    {
        builder.ToTable("tenant_settings", "chronith");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.PrimaryColor)
            .IsRequired()
            .HasMaxLength(7);

        builder.Property(s => s.AccentColor)
            .HasMaxLength(7);

        builder.Property(s => s.LogoUrl)
            .HasMaxLength(2048);

        builder.Property(s => s.CustomDomain)
            .HasMaxLength(253);

        builder.Property(s => s.WelcomeMessage)
            .HasMaxLength(500);

        builder.Property(s => s.TermsUrl)
            .HasMaxLength(2048);

        builder.Property(s => s.PrivacyUrl)
            .HasMaxLength(2048);

        builder.HasIndex(s => s.TenantId).IsUnique();

        builder.Property(s => s.Version)
            .IsRowVersion()
            .IsConcurrencyToken();
    }
}
