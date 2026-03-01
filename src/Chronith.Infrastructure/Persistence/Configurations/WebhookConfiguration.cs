using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class WebhookConfiguration : IEntityTypeConfiguration<WebhookEntity>
{
    public void Configure(EntityTypeBuilder<WebhookEntity> builder)
    {
        builder.ToTable("webhooks", "chronith");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Url)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(w => w.Secret)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(w => new { w.TenantId, w.IsDeleted });
        builder.HasIndex(w => new { w.TenantId, w.BookingTypeId });
    }
}
