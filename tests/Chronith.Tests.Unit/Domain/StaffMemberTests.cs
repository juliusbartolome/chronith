using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class StaffMemberTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var staff = StaffMember.Create(
            tenantId: tenantId,
            tenantUserId: userId,
            name: "Alice",
            email: "alice@example.com",
            availabilityWindows:
            [
                new StaffAvailabilityWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
                new StaffAvailabilityWindow(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 0))
            ]);

        staff.Id.Should().NotBeEmpty();
        staff.TenantId.Should().Be(tenantId);
        staff.TenantUserId.Should().Be(userId);
        staff.Name.Should().Be("Alice");
        staff.Email.Should().Be("alice@example.com");
        staff.IsActive.Should().BeTrue();
        staff.IsDeleted.Should().BeFalse();
        staff.AvailabilityWindows.Should().HaveCount(2);
    }

    [Fact]
    public void Create_WithoutTenantUserId_AllowsExternalStaff()
    {
        var staff = StaffMember.Create(
            tenantId: Guid.NewGuid(),
            tenantUserId: null,
            name: "External Bob",
            email: "bob@external.com",
            availabilityWindows: []);

        staff.TenantUserId.Should().BeNull();
        staff.Name.Should().Be("External Bob");
    }

    [Fact]
    public void Update_ChangesNameEmailAndWindows()
    {
        var staff = StaffMember.Create(
            tenantId: Guid.NewGuid(),
            tenantUserId: null,
            name: "Old Name",
            email: "old@example.com",
            availabilityWindows: []);

        staff.Update(
            name: "New Name",
            email: "new@example.com",
            availabilityWindows:
            [
                new StaffAvailabilityWindow(DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(16, 0))
            ]);

        staff.Name.Should().Be("New Name");
        staff.Email.Should().Be("new@example.com");
        staff.AvailabilityWindows.Should().HaveCount(1);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var staff = StaffMember.Create(
            tenantId: Guid.NewGuid(),
            tenantUserId: null,
            name: "Test",
            email: "test@example.com",
            availabilityWindows: []);

        staff.Deactivate();

        staff.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var staff = StaffMember.Create(
            tenantId: Guid.NewGuid(),
            tenantUserId: null,
            name: "Test",
            email: "test@example.com",
            availabilityWindows: []);

        staff.Deactivate();
        staff.Activate();

        staff.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SoftDelete_SetsIsDeletedTrue()
    {
        var staff = StaffMember.Create(
            tenantId: Guid.NewGuid(),
            tenantUserId: null,
            name: "Test",
            email: "test@example.com",
            availabilityWindows: []);

        staff.SoftDelete();

        staff.IsDeleted.Should().BeTrue();
    }
}
