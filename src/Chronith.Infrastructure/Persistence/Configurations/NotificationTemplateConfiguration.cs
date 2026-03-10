using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplateEntity>
{
    public void Configure(EntityTypeBuilder<NotificationTemplateEntity> builder)
    {
        builder.ToTable("notification_templates", "chronith");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.ChannelType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Subject)
            .HasMaxLength(500);

        builder.Property(t => t.Body)
            .IsRequired()
            .HasMaxLength(10000);

        builder.HasIndex(t => new { t.TenantId, t.EventType, t.ChannelType })
            .HasDatabaseName("ix_notification_templates_event_channel")
            .IsUnique();

        builder.HasIndex(t => new { t.TenantId, t.IsDeleted });
    }
}
