using Chronith.Domain.Enums;
using Chronith.Tests.Integration.Fixtures;
using Chronith.Tests.Integration.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class BookingConflictQueryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset BaseDate =
        new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConflictQuery_ReturnsZero_WhenNoOverlappingBookings()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        _ = await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, tenantId);

        // Book noon–1pm
        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: BaseDate.AddHours(12),
            end: BaseDate.AddHours(13));

        // Act — query at 3pm–4pm (no overlap)
        var newStart = BaseDate.AddHours(15);
        var newEnd = BaseDate.AddHours(16);

        var count = await db.Bookings
            .AsNoTracking()
            .CountAsync(b =>
                b.BookingTypeId == bookingTypeId &&
                b.Start < newEnd &&
                b.End > newStart &&
                b.Status != BookingStatus.Cancelled &&
                !b.IsDeleted);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task ConflictQuery_ReturnsCount_WhenOverlapExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        _ = await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, tenantId);

        // Two overlapping bookings: 10am–11am and 10:30am–11:30am
        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: BaseDate.AddHours(10),
            end: BaseDate.AddHours(11),
            customerId: "cust-a");

        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: BaseDate.AddHours(10).AddMinutes(30),
            end: BaseDate.AddHours(11).AddMinutes(30),
            customerId: "cust-b");

        // Act — new booking at 10am–11am overlaps both
        var newStart = BaseDate.AddHours(10);
        var newEnd = BaseDate.AddHours(11);

        var count = await db.Bookings
            .AsNoTracking()
            .CountAsync(b =>
                b.BookingTypeId == bookingTypeId &&
                b.Start < newEnd &&
                b.End > newStart &&
                b.Status != BookingStatus.Cancelled &&
                !b.IsDeleted);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task ConflictQuery_IgnoresCancelledBookings()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        _ = await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, tenantId);

        // Cancelled booking at noon
        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: BaseDate.AddHours(12),
            end: BaseDate.AddHours(13),
            status: BookingStatus.Cancelled);

        // Act — query overlapping that slot
        var newStart = BaseDate.AddHours(12);
        var newEnd = BaseDate.AddHours(13);

        var count = await db.Bookings
            .AsNoTracking()
            .CountAsync(b =>
                b.BookingTypeId == bookingTypeId &&
                b.Start < newEnd &&
                b.End > newStart &&
                b.Status != BookingStatus.Cancelled &&
                !b.IsDeleted);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task ConflictQuery_IncludesPendingPaymentBookings()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        _ = await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, tenantId);

        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: BaseDate.AddHours(9),
            end: BaseDate.AddHours(10),
            status: BookingStatus.PendingPayment);

        // Act — query overlapping that slot
        var newStart = BaseDate.AddHours(9);
        var newEnd = BaseDate.AddHours(10);

        var count = await db.Bookings
            .AsNoTracking()
            .CountAsync(b =>
                b.BookingTypeId == bookingTypeId &&
                b.Start < newEnd &&
                b.End > newStart &&
                b.Status != BookingStatus.Cancelled &&
                !b.IsDeleted);

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public async Task ConflictQuery_IncludesPendingVerificationBookings()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        _ = await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, tenantId);

        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: BaseDate.AddHours(14),
            end: BaseDate.AddHours(15),
            status: BookingStatus.PendingVerification);

        // Act — query overlapping that slot
        var newStart = BaseDate.AddHours(14);
        var newEnd = BaseDate.AddHours(15);

        var count = await db.Bookings
            .AsNoTracking()
            .CountAsync(b =>
                b.BookingTypeId == bookingTypeId &&
                b.Start < newEnd &&
                b.End > newStart &&
                b.Status != BookingStatus.Cancelled &&
                !b.IsDeleted);

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public async Task ConflictQuery_RespectsBufferAdjustedRange()
    {
        // Arrange — simulate a 15-minute buffer before/after that widens the query range
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        _ = await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        // BookingType has 15-min buffers
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, tenantId, durationMinutes: 60);

        // Existing booking: noon–1pm
        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: BaseDate.AddHours(12),
            end: BaseDate.AddHours(13));

        // New booking: 1pm–2pm, but with 15-min buffer before, effective query range is 12:45–2pm
        // The existing booking ends at 1pm which is > adjusted start 12:45, so it conflicts
        var bufferBefore = TimeSpan.FromMinutes(15);
        var bufferAfter = TimeSpan.FromMinutes(15);
        var newStart = BaseDate.AddHours(13); // 1pm
        var newEnd = BaseDate.AddHours(14);   // 2pm

        var adjustedStart = newStart - bufferBefore;  // 12:45pm
        var adjustedEnd = newEnd + bufferAfter;        // 2:15pm

        var count = await db.Bookings
            .AsNoTracking()
            .CountAsync(b =>
                b.BookingTypeId == bookingTypeId &&
                b.Start < adjustedEnd &&
                b.End > adjustedStart &&
                b.Status != BookingStatus.Cancelled &&
                !b.IsDeleted);

        // Assert — existing booking at noon–1pm overlaps the adjusted range 12:45–2:15pm
        count.Should().Be(1);
    }
}
