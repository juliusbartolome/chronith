using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKeyEntity>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyEntity> builder)
    {
        builder.ToTable("idempotency_keys", "chronith");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Key)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(k => k.EndpointRoute)
            .IsRequired();

        builder.Property(k => k.RequestHash)
            .IsRequired();

        builder.Property(k => k.ResponseBody)
            .HasColumnType("text")
            .HasMaxLength(65536);

        builder.Property(k => k.ResponseContentType)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(k => new { k.TenantId, k.Key, k.EndpointRoute })
            .IsUnique();

        builder.HasIndex(k => k.ExpiresAt);
    }
}
