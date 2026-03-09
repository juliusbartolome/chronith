using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class AuditEntryTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var entry = AuditEntry.Create(
            tenantId: tenantId,
            userId: "user-123",
            userRole: "Admin",
            entityType: "Booking",
            entityId: entityId,
            action: "Created",
            oldValues: null,
            newValues: """{"status":"PendingPayment"}""",
            metadata: """{"ip":"127.0.0.1"}""");

        var after = DateTimeOffset.UtcNow;

        entry.Id.Should().NotBeEmpty();
        entry.TenantId.Should().Be(tenantId);
        entry.UserId.Should().Be("user-123");
        entry.UserRole.Should().Be("Admin");
        entry.EntityType.Should().Be("Booking");
        entry.EntityId.Should().Be(entityId);
        entry.Action.Should().Be("Created");
        entry.OldValues.Should().BeNull();
        entry.NewValues.Should().Be("""{"status":"PendingPayment"}""");
        entry.Metadata.Should().Be("""{"ip":"127.0.0.1"}""");
        entry.Timestamp.Should().BeOnOrAfter(before);
        entry.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var entry1 = AuditEntry.Create(
            tenantId: Guid.NewGuid(),
            userId: "user-1",
            userRole: "Admin",
            entityType: "Booking",
            entityId: Guid.NewGuid(),
            action: "Created",
            oldValues: null,
            newValues: null,
            metadata: null);

        var entry2 = AuditEntry.Create(
            tenantId: Guid.NewGuid(),
            userId: "user-2",
            userRole: "Staff",
            entityType: "Booking",
            entityId: Guid.NewGuid(),
            action: "Updated",
            oldValues: null,
            newValues: null,
            metadata: null);

        entry1.Id.Should().NotBe(entry2.Id);
    }

    [Fact]
    public void Create_WithNullOptionalFields_SetsNulls()
    {
        var entry = AuditEntry.Create(
            tenantId: Guid.NewGuid(),
            userId: "user-1",
            userRole: "Admin",
            entityType: "Booking",
            entityId: Guid.NewGuid(),
            action: "Deleted",
            oldValues: null,
            newValues: null,
            metadata: null);

        entry.OldValues.Should().BeNull();
        entry.NewValues.Should().BeNull();
        entry.Metadata.Should().BeNull();
    }
}
