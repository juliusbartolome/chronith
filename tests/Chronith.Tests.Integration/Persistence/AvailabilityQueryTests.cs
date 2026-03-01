using Chronith.Domain.Enums;
using Chronith.Tests.Integration.Fixtures;
using Chronith.Tests.Integration.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class AvailabilityQueryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset BaseDate =
        new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AvailabilityQuery_ProjectsOnlyStartAndEnd_NoFullEntity()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.Container.GetConnectionString(), tenantId, applyMigrations: true);

        await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, tenantId);

        var expectedStart = BaseDate.AddHours(9);
        var expectedEnd = BaseDate.AddHours(10);
        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: expectedStart, end: expectedEnd);

        // Act — project only Start and End (anonymous type projection)
        var slots = await db.Bookings
            .AsNoTracking()
            .Where(b => b.BookingTypeId == bookingTypeId)
            .Select(b => new { b.Start, b.End })
            .ToListAsync();

        // Assert
        slots.Should().HaveCount(1);
        slots[0].Start.Should().Be(expectedStart);
        slots[0].End.Should().Be(expectedEnd);
    }

    [Fact]
    public async Task AvailabilityQuery_FiltersByTenantId()
    {
        // Arrange — two separate tenants, each with their own booking
        var tenantIdA = Guid.NewGuid();
        var tenantIdB = Guid.NewGuid();

        await using var dbA = await DbContextFactory.CreateAsync(
            postgres.Container.GetConnectionString(), tenantIdA, applyMigrations: true);
        await using var dbB = await DbContextFactory.CreateAsync(
            postgres.Container.GetConnectionString(), tenantIdB);

        await SeedData.SeedTenantAsync(dbA, $"tenant-{tenantIdA:N}");
        await SeedData.SeedTenantAsync(dbB, $"tenant-{tenantIdB:N}");

        var bookingTypeIdA = await SeedData.SeedBookingTypeAsync(dbA, tenantIdA, slug: "type-a");
        var bookingTypeIdB = await SeedData.SeedBookingTypeAsync(dbB, tenantIdB, slug: "type-b");

        await SeedData.SeedBookingAsync(dbA, tenantIdA, bookingTypeIdA,
            start: BaseDate.AddHours(9), end: BaseDate.AddHours(10));
        await SeedData.SeedBookingAsync(dbB, tenantIdB, bookingTypeIdB,
            start: BaseDate.AddHours(11), end: BaseDate.AddHours(12));

        // Act — context A's query filter isolates tenantA's bookings
        var slotsA = await dbA.Bookings
            .AsNoTracking()
            .Select(b => new { b.Start, b.End })
            .ToListAsync();

        // Assert — only tenant A's booking visible through context A
        slotsA.Should().HaveCount(1);
        slotsA[0].Start.Should().Be(BaseDate.AddHours(9));
    }

    [Fact]
    public async Task AvailabilityQuery_FiltersByBookingTypeId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.Container.GetConnectionString(), tenantId, applyMigrations: true);

        await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        var bookingTypeId1 = await SeedData.SeedBookingTypeAsync(db, tenantId, slug: "type-1");
        var bookingTypeId2 = await SeedData.SeedBookingTypeAsync(db, tenantId, slug: "type-2");

        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId1,
            start: BaseDate.AddHours(9), end: BaseDate.AddHours(10), customerId: "cust-type1");
        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId2,
            start: BaseDate.AddHours(11), end: BaseDate.AddHours(12), customerId: "cust-type2");

        // Act — filter by bookingTypeId1 only
        var slots = await db.Bookings
            .AsNoTracking()
            .Where(b => b.BookingTypeId == bookingTypeId1)
            .Select(b => new { b.Start, b.End, b.BookingTypeId })
            .ToListAsync();

        // Assert
        slots.Should().HaveCount(1);
        slots[0].BookingTypeId.Should().Be(bookingTypeId1);
        slots[0].Start.Should().Be(BaseDate.AddHours(9));
    }

    [Fact]
    public async Task AvailabilityQuery_OnlyReturnsConflictingStatuses()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.Container.GetConnectionString(), tenantId, applyMigrations: true);

        await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, tenantId);

        // Seed one of each status
        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: BaseDate.AddHours(9), end: BaseDate.AddHours(10),
            status: BookingStatus.Confirmed, customerId: "cust-confirmed");

        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: BaseDate.AddHours(10), end: BaseDate.AddHours(11),
            status: BookingStatus.PendingPayment, customerId: "cust-pending-pay");

        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: BaseDate.AddHours(11), end: BaseDate.AddHours(12),
            status: BookingStatus.PendingVerification, customerId: "cust-pending-verif");

        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: BaseDate.AddHours(12), end: BaseDate.AddHours(13),
            status: BookingStatus.Cancelled, customerId: "cust-cancelled");

        // Act — query excluding cancelled (the conflict-relevant statuses)
        var slots = await db.Bookings
            .AsNoTracking()
            .Where(b =>
                b.BookingTypeId == bookingTypeId &&
                b.Status != BookingStatus.Cancelled)
            .Select(b => new { b.Start, b.End, b.Status })
            .ToListAsync();

        // Assert — 3 statuses returned, Cancelled excluded
        slots.Should().HaveCount(3);
        slots.Should().NotContain(s => s.Status == BookingStatus.Cancelled);
        slots.Should().Contain(s => s.Status == BookingStatus.Confirmed);
        slots.Should().Contain(s => s.Status == BookingStatus.PendingPayment);
        slots.Should().Contain(s => s.Status == BookingStatus.PendingVerification);
    }
}
