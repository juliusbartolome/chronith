using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class TimeBlockTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var bookingTypeId = Guid.NewGuid();
        var staffMemberId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var end = start.AddHours(4);

        var block = TimeBlock.Create(tenantId, bookingTypeId, staffMemberId, start, end, "Vacation");

        block.Id.Should().NotBeEmpty();
        block.TenantId.Should().Be(tenantId);
        block.BookingTypeId.Should().Be(bookingTypeId);
        block.StaffMemberId.Should().Be(staffMemberId);
        block.Start.Should().Be(start);
        block.End.Should().Be(end);
        block.Reason.Should().Be("Vacation");
        block.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_WithNullOptionalFields_Succeeds()
    {
        var block = TimeBlock.Create(
            Guid.NewGuid(), null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null);

        block.BookingTypeId.Should().BeNull();
        block.StaffMemberId.Should().BeNull();
        block.Reason.Should().BeNull();
    }

    [Fact]
    public void SoftDelete_SetsIsDeletedTrue()
    {
        var block = TimeBlock.Create(
            Guid.NewGuid(), null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null);

        block.SoftDelete();

        block.IsDeleted.Should().BeTrue();
    }
}
