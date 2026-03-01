using Chronith.Domain.Enums;
using Chronith.Infrastructure.Persistence;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Tests.Integration.Helpers;

public static class SeedData
{
    public static async Task<Guid> SeedTenantAsync(ChronithDbContext db, string slug = "test-tenant")
    {
        var id = Guid.NewGuid();
        db.Tenants.Add(new TenantEntity
        {
            Id = id,
            Slug = slug,
            Name = "Test Tenant",
            TimeZoneId = "UTC",
            IsDeleted = false
        });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<Guid> SeedBookingTypeAsync(
        ChronithDbContext db,
        Guid tenantId,
        BookingKind kind = BookingKind.TimeSlot,
        string slug = "test-type",
        int capacity = 1,
        int durationMinutes = 60)
    {
        var id = Guid.NewGuid();
        db.BookingTypes.Add(new BookingTypeEntity
        {
            Id = id,
            TenantId = tenantId,
            Slug = slug,
            Name = "Test Type",
            Kind = kind,
            Capacity = capacity,
            PaymentMode = PaymentMode.Manual,
            IsDeleted = false,
            DurationMinutes = durationMinutes,
            BufferBeforeMinutes = 0,
            BufferAfterMinutes = 0
        });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<Guid> SeedBookingAsync(
        ChronithDbContext db,
        Guid tenantId,
        Guid bookingTypeId,
        DateTimeOffset start,
        DateTimeOffset end,
        BookingStatus status = BookingStatus.Confirmed,
        string customerId = "cust-1",
        bool isDeleted = false)
    {
        var id = Guid.NewGuid();
        db.Bookings.Add(new BookingEntity
        {
            Id = id,
            TenantId = tenantId,
            BookingTypeId = bookingTypeId,
            Start = start,
            End = end,
            Status = status,
            CustomerId = customerId,
            CustomerEmail = $"{customerId}@example.com",
            IsDeleted = isDeleted
        });
        await db.SaveChangesAsync();
        return id;
    }
}
