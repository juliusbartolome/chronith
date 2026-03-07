using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class BookingTypeStaffAssignmentConfiguration : IEntityTypeConfiguration<BookingTypeStaffAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<BookingTypeStaffAssignmentEntity> builder)
    {
        builder.ToTable("booking_type_staff_assignments", "chronith");

        builder.HasKey(a => new { a.BookingTypeId, a.StaffMemberId });

        builder.HasOne(a => a.BookingType)
            .WithMany(bt => bt.StaffAssignments)
            .HasForeignKey(a => a.BookingTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.StaffMember)
            .WithMany(s => s.BookingTypeAssignments)
            .HasForeignKey(a => a.StaffMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
