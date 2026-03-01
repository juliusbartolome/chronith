using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class AvailabilityWindowConfiguration : IEntityTypeConfiguration<AvailabilityWindowEntity>
{
    public void Configure(EntityTypeBuilder<AvailabilityWindowEntity> builder)
    {
        builder.ToTable("availability_windows", "chronith");

        builder.HasKey(w => w.Id);

        builder.HasIndex(w => w.BookingTypeId);
    }
}
