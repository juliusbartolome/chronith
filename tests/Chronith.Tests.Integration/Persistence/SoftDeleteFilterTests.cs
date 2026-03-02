using Chronith.Tests.Integration.Fixtures;
using Chronith.Tests.Integration.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Persistence;

[Collection("Integration")]
public sealed class SoftDeleteFilterTests(PostgresFixture postgres)
{
    [Fact]
    public async Task DeletedBookings_NotReturnedByDefaultQuery()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, tenantId);

        var start = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: start, end: start.AddHours(1), isDeleted: true);

        // Act — default query applies the global filter (!IsDeleted && TenantId match)
        var count = await db.Bookings.AsNoTracking().CountAsync();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task IgnoreQueryFilters_ReturnsDeletedRecords()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, tenantId);

        var start = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var deletedId = await SeedData.SeedBookingAsync(db, tenantId, bookingTypeId,
            start: start, end: start.AddHours(1), isDeleted: true);

        // Act — bypass global query filter
        var found = await db.Bookings
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == deletedId);

        // Assert
        found.Should().NotBeNull();
        found!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeletedBookingTypes_NotReturnedByDefaultQuery()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var db = await DbContextFactory.CreateAsync(
            postgres.ConnectionString, tenantId, applyMigrations: true);

        await SeedData.SeedTenantAsync(db, $"tenant-{tenantId:N}");

        // Seed a deleted booking type directly
        var deletedTypeId = Guid.NewGuid();
        db.BookingTypes.Add(new Chronith.Infrastructure.Persistence.Entities.BookingTypeEntity
        {
            Id = deletedTypeId,
            TenantId = tenantId,
            Slug = "deleted-type",
            Name = "Deleted Type",
            Kind = Chronith.Domain.Enums.BookingKind.TimeSlot,
            Capacity = 1,
            PaymentMode = Chronith.Domain.Enums.PaymentMode.Manual,
            IsDeleted = true,
            DurationMinutes = 60,
            BufferBeforeMinutes = 0,
            BufferAfterMinutes = 0
        });
        await db.SaveChangesAsync();

        // Act — default query applies the global filter
        var count = await db.BookingTypes.AsNoTracking().CountAsync();

        // Assert
        count.Should().Be(0);
    }
}
