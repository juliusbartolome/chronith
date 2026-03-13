using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class TenantPlanConfiguration : IEntityTypeConfiguration<TenantPlanEntity>
{
    public void Configure(EntityTypeBuilder<TenantPlanEntity> builder)
    {
        builder.ToTable("tenant_plans", "chronith");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.MaxBookingTypes)
            .IsRequired();

        builder.Property(p => p.MaxStaffMembers)
            .IsRequired();

        builder.Property(p => p.MaxBookingsPerMonth)
            .IsRequired();

        builder.Property(p => p.MaxCustomers)
            .IsRequired();

        builder.Property(p => p.PriceCentavos)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .IsRequired();

        builder.Property(p => p.SortOrder)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.RowVersion)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid");

        builder.HasIndex(p => p.IsActive);
        builder.HasIndex(p => p.SortOrder);
        builder.HasIndex(p => p.Name).IsUnique();
    }
}
