using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMemberEntity>
{
    public void Configure(EntityTypeBuilder<StaffMemberEntity> builder)
    {
        builder.ToTable("staff_members", "chronith");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.HasIndex(s => new { s.TenantId, s.IsDeleted });

        builder.HasMany(s => s.AvailabilityWindows)
            .WithOne(w => w.StaffMember)
            .HasForeignKey(w => w.StaffMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.BookingTypeAssignments)
            .WithOne(a => a.StaffMember)
            .HasForeignKey(a => a.StaffMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
