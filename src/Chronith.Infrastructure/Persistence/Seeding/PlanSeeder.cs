using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chronith.Infrastructure.Persistence.Seeding;

public sealed class PlanSeeder(ChronithDbContext db, ILogger<PlanSeeder> logger) : IPlanSeeder
{
    public static readonly Guid FreePlanId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid StarterPlanId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid ProPlanId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid EnterprisePlanId = Guid.Parse("00000000-0000-0000-0000-000000000004");

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var plans = DefaultPlans();

        foreach (var plan in plans)
        {
            var exists = await db.TenantPlans
                .IgnoreQueryFilters()
                .AnyAsync(p => p.Id == plan.Id, ct);

            if (!exists)
            {
                await db.TenantPlans.AddAsync(plan, ct);
                logger.LogInformation("Seeding plan {PlanName} ({PlanId})", plan.Name, plan.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public static IReadOnlyList<TenantPlanEntity> DefaultPlans() =>
    [
        new TenantPlanEntity
        {
            Id = FreePlanId,
            Name = "Free",
            MaxBookingTypes = 1,
            MaxStaffMembers = 0,
            MaxBookingsPerMonth = 50,
            MaxCustomers = 50,
            NotificationsEnabled = false,
            AnalyticsEnabled = false,
            CustomBrandingEnabled = false,
            ApiAccessEnabled = false,
            AuditLogEnabled = false,
            PriceCentavos = 0,
            IsActive = true,
            SortOrder = 0,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            IsDeleted = false,
        },
        new TenantPlanEntity
        {
            Id = StarterPlanId,
            Name = "Starter",
            MaxBookingTypes = 5,
            MaxStaffMembers = 3,
            MaxBookingsPerMonth = 500,
            MaxCustomers = 500,
            NotificationsEnabled = true,
            AnalyticsEnabled = false,
            CustomBrandingEnabled = false,
            ApiAccessEnabled = false,
            AuditLogEnabled = false,
            PriceCentavos = 190000,
            IsActive = true,
            SortOrder = 1,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            IsDeleted = false,
        },
        new TenantPlanEntity
        {
            Id = ProPlanId,
            Name = "Pro",
            MaxBookingTypes = 25,
            MaxStaffMembers = 15,
            MaxBookingsPerMonth = 5000,
            MaxCustomers = 5000,
            NotificationsEnabled = true,
            AnalyticsEnabled = true,
            CustomBrandingEnabled = true,
            ApiAccessEnabled = true,
            AuditLogEnabled = false,
            PriceCentavos = 490000,
            IsActive = true,
            SortOrder = 2,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            IsDeleted = false,
        },
        new TenantPlanEntity
        {
            Id = EnterprisePlanId,
            Name = "Enterprise",
            MaxBookingTypes = int.MaxValue,
            MaxStaffMembers = int.MaxValue,
            MaxBookingsPerMonth = int.MaxValue,
            MaxCustomers = int.MaxValue,
            NotificationsEnabled = true,
            AnalyticsEnabled = true,
            CustomBrandingEnabled = true,
            ApiAccessEnabled = true,
            AuditLogEnabled = true,
            PriceCentavos = 1490000,
            IsActive = true,
            SortOrder = 3,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            IsDeleted = false,
        },
    ];
}
