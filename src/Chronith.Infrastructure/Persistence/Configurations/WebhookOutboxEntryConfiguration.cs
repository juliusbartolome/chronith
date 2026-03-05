using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class WebhookOutboxEntryConfiguration : IEntityTypeConfiguration<WebhookOutboxEntryEntity>
{
    public void Configure(EntityTypeBuilder<WebhookOutboxEntryEntity> builder)
    {
        builder.ToTable("webhook_outbox_entries", "chronith");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Payload).IsRequired();
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(e => e.RetryRequestedAt).IsRequired(false);

        // Dispatcher poll index
        builder.HasIndex(e => new { e.Status, e.NextRetryAt })
            .HasDatabaseName("IX_webhook_outbox_entries_Status_NextRetryAt");

        // Lookup by webhook
        builder.HasIndex(e => e.WebhookId)
            .HasDatabaseName("IX_webhook_outbox_entries_WebhookId");

        // Trace by booking
        builder.HasIndex(e => e.BookingId)
            .HasDatabaseName("IX_webhook_outbox_entries_BookingId");

        // WebhookId is nullable — null for CustomerCallback entries
        builder.Property(e => e.WebhookId).IsRequired(false);

        // BookingTypeId is nullable — set for CustomerCallback entries only
        builder.Property(e => e.BookingTypeId).IsRequired(false);

        // Category: 0 = TenantWebhook, 1 = CustomerCallback
        builder.Property(e => e.Category)
            .HasDefaultValue(0)
            .IsRequired();

        // Index for customer callback lookup by booking type
        builder.HasIndex(e => e.BookingTypeId)
            .HasDatabaseName("IX_webhook_outbox_entries_BookingTypeId");
    }
}
