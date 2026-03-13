using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscriptionEntity>
{
    public void Configure(EntityTypeBuilder<TenantSubscriptionEntity> builder)
    {
        builder.ToTable("tenant_subscriptions", "chronith");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.Property(s => s.PlanId)
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(s => s.PaymentProviderSubscriptionId)
            .HasMaxLength(255);

        builder.Property(s => s.CancelReason)
            .HasMaxLength(500);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.RowVersion)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");

        builder.HasIndex(s => new { s.TenantId, s.IsDeleted });
        builder.HasIndex(s => new { s.TenantId, s.Status });
        builder.HasIndex(s => s.PlanId);
    }
}
