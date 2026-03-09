using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Infrastructure.Persistence;
using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Tests.Functional.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chronith.Tests.Functional.Helpers;

public static class SeedData
{
    private sealed class StubTenantContext : ITenantContext
    {
        public Guid TenantId { get; }
        public string UserId => TestConstants.AdminUserId;
        public string Role => "TenantAdmin";
        public StubTenantContext(Guid tenantId) => TenantId = tenantId;
    }

    public static ChronithDbContext CreateDbContext(
        WebApplicationFactory<Program> factory,
        Guid? tenantId = null)
    {
        var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<ChronithDbContext>>();
        return new ChronithDbContext(options, new StubTenantContext(tenantId ?? TestConstants.TenantId));
    }

    public static async Task<Guid> SeedTenantAsync(ChronithDbContext db, Guid? id = null, string slug = "test-tenant")
    {
        var tenantId = id ?? TestConstants.TenantId;
        // Use INSERT ... ON CONFLICT DO NOTHING to avoid race conditions when
        // multiple test classes seed the shared tenant concurrently.
        // No conflict target is specified so PostgreSQL suppresses violations
        // on any unique constraint ("Id" PK and "Slug" unique index).
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO chronith.tenants ("Id","Slug","Name","TimeZoneId","IsDeleted","CreatedAt")
            VALUES ({0},{1},{2},{3},{4},{5})
            ON CONFLICT DO NOTHING
            """, tenantId, slug, "Test Tenant", "UTC", false, DateTimeOffset.UtcNow);
        return tenantId;
    }

    public static async Task<Guid> SeedBookingTypeAsync(
        ChronithDbContext db,
        string slug = "test-type",
        int capacity = 5,
        int durationMinutes = 60,
        long priceInCentavos = 10_000,
        string currency = "PHP",
        PaymentMode paymentMode = PaymentMode.Manual,
        string? paymentProvider = null)
    {
        // Idempotent — return existing id if slug already seeded
        var existing = db.BookingTypes.IgnoreQueryFilters()
            .FirstOrDefault(bt => bt.Slug == slug && bt.TenantId == TestConstants.TenantId);
        if (existing is not null) return existing.Id;

        var id = Guid.NewGuid();
        db.BookingTypes.Add(new BookingTypeEntity
        {
            Id = id,
            TenantId = TestConstants.TenantId,
            Slug = slug,
            Name = "Test Type",
            Kind = BookingKind.TimeSlot,
            Capacity = capacity,
            PaymentMode = paymentMode,
            PaymentProvider = paymentProvider,
            IsDeleted = false,
            DurationMinutes = durationMinutes,
            BufferBeforeMinutes = 0,
            BufferAfterMinutes = 0,
            PriceInCentavos = priceInCentavos,
            Currency = currency
        });

        // Seed availability windows for all 7 days of the week (08:00–18:00)
        // so that any test start time during business hours resolves to a valid slot.
        foreach (var dow in Enum.GetValues<DayOfWeek>())
        {
            db.AvailabilityWindows.Add(new AvailabilityWindowEntity
            {
                Id = Guid.NewGuid(),
                BookingTypeId = id,
                DayOfWeek = (int)dow,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(18, 0)
            });
        }

        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<Guid> SeedBookingAsync(
        ChronithDbContext db,
        Guid bookingTypeId,
        DateTimeOffset start,
        DateTimeOffset end,
        BookingStatus status = BookingStatus.PendingPayment,
        string customerId = "cust-seed-1",
        long amountInCentavos = 0,
        Guid? staffMemberId = null)
    {
        var id = Guid.NewGuid();
        db.Bookings.Add(new BookingEntity
        {
            Id = id,
            TenantId = TestConstants.TenantId,
            BookingTypeId = bookingTypeId,
            Start = start,
            End = end,
            Status = status,
            CustomerId = customerId,
            CustomerEmail = $"{customerId}@example.com",
            AmountInCentavos = amountInCentavos,
            StaffMemberId = staffMemberId,
            IsDeleted = false
        });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<Guid> SeedStaffMemberAsync(
        ChronithDbContext db,
        string name = "Test Staff",
        string email = "staff@example.com")
    {
        var id = Guid.NewGuid();
        db.StaffMembers.Add(new StaffMemberEntity
        {
            Id = id,
            TenantId = TestConstants.TenantId,
            Name = name,
            Email = email,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<Guid> SeedWebhookAsync(
        ChronithDbContext db,
        Guid bookingTypeId,
        string url = "https://example.com/webhook",
        string secret = "webhook-secret-at-least-16chars")
    {
        var id = Guid.NewGuid();
        db.Webhooks.Add(new WebhookEntity
        {
            Id = id,
            TenantId = TestConstants.TenantId,
            BookingTypeId = bookingTypeId,
            Url = url,
            Secret = secret,
            IsDeleted = false
        });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<IReadOnlyList<Guid>> SeedOutboxEntriesAsync(
        ChronithDbContext db,
        Guid webhookId,
        int count = 3)
    {
        var ids = new List<Guid>();
        for (int i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            db.WebhookOutboxEntries.Add(new WebhookOutboxEntryEntity
            {
                Id = id,
                TenantId = TestConstants.TenantId,
                WebhookId = webhookId,
                BookingId = Guid.NewGuid(),
                EventType = "booking.confirmed",
                Payload = "{}",
                Status = OutboxStatus.Pending,
                AttemptCount = 0,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i),
            });
            ids.Add(id);
        }
        await db.SaveChangesAsync();
        return ids;
    }

    public static async Task<Guid> SeedFailedOutboxEntryAsync(
        ChronithDbContext db,
        Guid webhookId)
    {
        var id = Guid.NewGuid();
        db.WebhookOutboxEntries.Add(new WebhookOutboxEntryEntity
        {
            Id = id,
            TenantId = TestConstants.TenantId,
            WebhookId = webhookId,
            BookingId = Guid.NewGuid(),
            EventType = "booking.confirmed",
            Payload = "{}",
            Status = OutboxStatus.Failed,
            AttemptCount = 6,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<Guid> SeedWaitlistEntryAsync(
        ChronithDbContext db,
        Guid bookingTypeId,
        DateTimeOffset desiredStart,
        DateTimeOffset desiredEnd,
        WaitlistStatus status = WaitlistStatus.Waiting,
        string customerId = "cust-waitlist-1",
        DateTimeOffset? offeredAt = null,
        DateTimeOffset? expiresAt = null)
    {
        var id = Guid.NewGuid();
        db.WaitlistEntries.Add(new WaitlistEntryEntity
        {
            Id = id,
            TenantId = TestConstants.TenantId,
            BookingTypeId = bookingTypeId,
            CustomerId = customerId,
            CustomerEmail = $"{customerId}@example.com",
            DesiredStart = desiredStart,
            DesiredEnd = desiredEnd,
            Status = status,
            OfferedAt = offeredAt,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<Guid> SeedCustomerAsync(
        ChronithDbContext db,
        Guid? id = null,
        string email = "seeded-customer@example.com",
        string name = "Seeded Customer",
        string passwordHash = "$2a$11$dummyhashforseeding00000000000000000000000000000")
    {
        var customerId = id ?? Guid.NewGuid();
        db.Customers.Add(new CustomerEntity
        {
            Id = customerId,
            TenantId = TestConstants.TenantId,
            Email = email,
            Name = name,
            PasswordHash = passwordHash,
            AuthProvider = "BuiltIn",
            IsEmailVerified = false,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            LastLoginAt = null
        });
        await db.SaveChangesAsync();
        return customerId;
    }

    public static async Task<Guid> SeedTenantAuthConfigAsync(
        ChronithDbContext db,
        bool allowBuiltIn = true,
        bool magicLink = false,
        string? oidcIssuer = null,
        string? oidcClientId = null,
        string? oidcAudience = null)
    {
        // Idempotent — return existing id if already seeded for this tenant
        var existing = db.TenantAuthConfigs.IgnoreQueryFilters()
            .FirstOrDefault(c => c.TenantId == TestConstants.TenantId);
        if (existing is not null) return existing.Id;

        var id = Guid.NewGuid();
        db.TenantAuthConfigs.Add(new TenantAuthConfigEntity
        {
            Id = id,
            TenantId = TestConstants.TenantId,
            AllowBuiltInAuth = allowBuiltIn,
            OidcIssuer = oidcIssuer,
            OidcClientId = oidcClientId,
            OidcAudience = oidcAudience,
            MagicLinkEnabled = magicLink,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return id;
    }
}
