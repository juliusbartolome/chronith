using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class StaffAvailabilityWindowConfiguration : IEntityTypeConfiguration<StaffAvailabilityWindowEntity>
{
    public void Configure(EntityTypeBuilder<StaffAvailabilityWindowEntity> builder)
    {
        builder.ToTable("staff_availability_windows", "chronith");

        builder.HasKey(w => w.Id);

        builder.HasIndex(w => w.StaffMemberId);
    }
}
