using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class TimeBlockConfiguration : IEntityTypeConfiguration<TimeBlockEntity>
{
    public void Configure(EntityTypeBuilder<TimeBlockEntity> builder)
    {
        builder.ToTable("time_blocks", "chronith");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Reason)
            .HasMaxLength(500);

        builder.HasIndex(t => new { t.TenantId, t.Start, t.End });
        builder.HasIndex(t => new { t.TenantId, t.IsDeleted });
    }
}
