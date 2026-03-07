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
    // No global query filter: webhook_outbox_entries is accessed by the
    // WebhookDispatcherService background worker, which requires cross-tenant access.
    public DbSet<WebhookOutboxEntryEntity> WebhookOutboxEntries => Set<WebhookOutboxEntryEntity>();
    public DbSet<TenantApiKeyEntity> TenantApiKeys => Set<TenantApiKeyEntity>();
    public DbSet<TenantUserEntity> TenantUsers => Set<TenantUserEntity>();
    public DbSet<TenantUserRefreshTokenEntity> TenantUserRefreshTokens => Set<TenantUserRefreshTokenEntity>();
    public DbSet<StaffMemberEntity> StaffMembers => Set<StaffMemberEntity>();
    public DbSet<StaffAvailabilityWindowEntity> StaffAvailabilityWindows => Set<StaffAvailabilityWindowEntity>();
    public DbSet<BookingTypeStaffAssignmentEntity> BookingTypeStaffAssignments => Set<BookingTypeStaffAssignmentEntity>();
    public DbSet<WaitlistEntryEntity> WaitlistEntries => Set<WaitlistEntryEntity>();
    public DbSet<TimeBlockEntity> TimeBlocks => Set<TimeBlockEntity>();

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

        modelBuilder.Entity<StaffMemberEntity>()
            .HasQueryFilter(s => !s.IsDeleted && s.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<WaitlistEntryEntity>()
            .HasQueryFilter(w => !w.IsDeleted && w.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<TimeBlockEntity>()
            .HasQueryFilter(t => !t.IsDeleted && t.TenantId == _tenantContext.TenantId);
    }
}
