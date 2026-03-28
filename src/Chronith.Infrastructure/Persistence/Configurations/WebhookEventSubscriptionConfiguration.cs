using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class WebhookEventSubscriptionConfiguration
    : IEntityTypeConfiguration<WebhookEventSubscriptionEntity>
{
    public void Configure(EntityTypeBuilder<WebhookEventSubscriptionEntity> builder)
    {
        builder.ToTable("webhook_event_subscriptions", "chronith");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventName)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(e => new { e.WebhookId, e.EventName }).IsUnique();

        builder.HasOne<WebhookEntity>()
            .WithMany(w => w.EventSubscriptions)
            .HasForeignKey(e => e.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
