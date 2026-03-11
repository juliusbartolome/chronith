using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class TenantNotificationConfigConfiguration
    : IEntityTypeConfiguration<TenantNotificationConfigEntity>
{
    public void Configure(EntityTypeBuilder<TenantNotificationConfigEntity> builder)
    {
        builder.ToTable("tenant_notification_configs", "chronith");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ChannelType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.Settings)
            .HasColumnType("text")
            .IsRequired();

        // One config per tenant per channel type
        builder.HasIndex(c => new { c.TenantId, c.ChannelType })
            .IsUnique()
            .HasDatabaseName("IX_tenant_notification_configs_TenantId_ChannelType");

        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_tenant_notification_configs_TenantId");
    }
}
