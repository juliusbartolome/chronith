using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence;

public sealed class ChronithDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public ChronithDbContext(
        DbContextOptions<ChronithDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<BookingTypeEntity> BookingTypes => Set<BookingTypeEntity>();
    public DbSet<BookingEntity> Bookings => Set<BookingEntity>();
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<WebhookEntity> Webhooks => Set<WebhookEntity>();
    public DbSet<AvailabilityWindowEntity> AvailabilityWindows => Set<AvailabilityWindowEntity>();
    public DbSet<BookingStatusChangeEntity> BookingStatusChanges => Set<BookingStatusChangeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("chronith");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChronithDbContext).Assembly);

        // Global query filters — tenant isolation + soft delete
        modelBuilder.Entity<BookingTypeEntity>()
            .HasQueryFilter(bt => !bt.IsDeleted && bt.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<BookingEntity>()
            .HasQueryFilter(b => !b.IsDeleted && b.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<WebhookEntity>()
            .HasQueryFilter(w => !w.IsDeleted && w.TenantId == _tenantContext.TenantId);
    }
}
