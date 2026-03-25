using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class TenantPaymentConfigConfiguration
    : IEntityTypeConfiguration<TenantPaymentConfigEntity>
{
    public void Configure(EntityTypeBuilder<TenantPaymentConfigEntity> builder)
    {
        builder.ToTable("tenant_payment_configs", "chronith");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ProviderName).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Label).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Settings).HasColumnType("text").IsRequired();
        builder.Property(c => c.PublicNote).HasColumnType("text");
        builder.Property(c => c.QrCodeUrl).HasColumnType("text");
        builder.Property(c => c.PaymentSuccessUrl).HasMaxLength(2048);
        builder.Property(c => c.PaymentFailureUrl).HasMaxLength(2048);

        // Unique label per tenant+provider (excluding deleted)
        builder.HasIndex(c => new { c.TenantId, c.ProviderName, c.Label })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("IX_tenant_payment_configs_TenantId_ProviderName_Label");

        // Only one active API config per tenant+provider (Manual allows multiple)
        builder.HasIndex(c => new { c.TenantId, c.ProviderName })
            .IsUnique()
            .HasFilter("\"IsActive\" = true AND \"IsDeleted\" = false AND \"ProviderName\" != 'Manual'")
            .HasDatabaseName("IX_tenant_payment_configs_TenantId_ProviderName_active");

        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_tenant_payment_configs_TenantId");
    }
}
