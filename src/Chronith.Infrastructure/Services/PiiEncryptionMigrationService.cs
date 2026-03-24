using Chronith.Application.Interfaces;
using Chronith.Application.Services;
using Chronith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chronith.Infrastructure.Services;

/// <summary>
/// Encrypts all existing plaintext PII rows at application startup.
///
/// Runs during StartAsync (before the app serves requests) so that
/// all plaintext rows are encrypted before the first real request.
///
/// Strategy: for each table, scan rows where the encrypted column is NULL
/// (not yet migrated) or where the plaintext column doesn't start with a
/// version prefix (legacy row). Encrypt and write in batches of 200.
///
/// Safe to run multiple times — already-encrypted rows are skipped.
/// </summary>
public sealed class PiiEncryptionMigrationService(
    IServiceScopeFactory scopeFactory,
    ILogger<PiiEncryptionMigrationService> logger
) : IHostedService
{
    private const int BatchSize = 200;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("PiiEncryptionMigrationService: starting PII encryption migration.");

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ChronithDbContext>();
            var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
            var blindIndex = scope.ServiceProvider.GetRequiredService<IBlindIndexService>();

            var totalMigrated = 0;
            totalMigrated += await MigrateCustomersAsync(db, encryption, blindIndex, cancellationToken);
            totalMigrated += await MigrateTenantUsersAsync(db, encryption, blindIndex, cancellationToken);
            totalMigrated += await MigrateBookingEmailsAsync(db, encryption, cancellationToken);
            totalMigrated += await MigrateWaitlistEmailsAsync(db, encryption, cancellationToken);
            totalMigrated += await MigrateStaffEmailsAsync(db, encryption, cancellationToken);
            totalMigrated += await MigrateBookingTypeSecretsAsync(db, encryption, cancellationToken);

            if (totalMigrated > 0)
                logger.LogInformation(
                    "PiiEncryptionMigrationService: PII encryption migration completed; plaintext rows were migrated.");
            else
                logger.LogDebug("PiiEncryptionMigrationService: no plaintext rows found. Nothing to migrate.");
        
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Log but don't crash — legacy plaintext fallback handles unencrypted rows
            logger.LogError(ex, "PiiEncryptionMigrationService: error during migration. " +
                "Plaintext rows remain; legacy fallback is active.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<int> MigrateCustomersAsync(
        ChronithDbContext db,
        IEncryptionService encryption,
        IBlindIndexService blindIndex,
        CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            var rows = await db.Customers
                .IgnoreQueryFilters()
                .Where(c => !c.IsDeleted && c.EmailEncrypted == null)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                row.EmailEncrypted = encryption.Encrypt(row.Email) ?? string.Empty;
                row.EmailToken = blindIndex.ComputeToken(row.Email);
                if (row.Mobile is not null && row.MobileEncrypted is null)
                    row.MobileEncrypted = encryption.Encrypt(row.Mobile);
            }

            await db.SaveChangesAsync(ct);
            total += rows.Count;
            logger.LogDebug("PiiEncryptionMigrationService: migrated {Count} customer rows.", rows.Count);

            if (rows.Count < BatchSize) break;
        }
        return total;
    }

    private async Task<int> MigrateTenantUsersAsync(
        ChronithDbContext db,
        IEncryptionService encryption,
        IBlindIndexService blindIndex,
        CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            var rows = await db.TenantUsers
                .IgnoreQueryFilters()
                .Where(u => u.EmailEncrypted == null)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                row.EmailEncrypted = encryption.Encrypt(row.Email) ?? string.Empty;
                row.EmailToken = blindIndex.ComputeToken(row.Email);
            }

            await db.SaveChangesAsync(ct);
            total += rows.Count;
            if (rows.Count < BatchSize) break;
        }
        return total;
    }

    private async Task<int> MigrateBookingEmailsAsync(
        ChronithDbContext db,
        IEncryptionService encryption,
        CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            // Not yet encrypted = doesn't start with a known version prefix
            var rows = await db.Bookings
                .IgnoreQueryFilters()
                .Where(b => !b.IsDeleted && !b.CustomerEmail.StartsWith("v1:"))
                .Take(BatchSize)
                .ToListAsync(ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
                row.CustomerEmail = encryption.Encrypt(row.CustomerEmail)
                    ?? throw new InvalidOperationException("Encryption returned null for a non-null booking customer email.");

            await db.SaveChangesAsync(ct);
            total += rows.Count;
            if (rows.Count < BatchSize) break;
        }
        return total;
    }

    private async Task<int> MigrateWaitlistEmailsAsync(
        ChronithDbContext db,
        IEncryptionService encryption,
        CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            var rows = await db.WaitlistEntries
                .IgnoreQueryFilters()
                .Where(w => !w.IsDeleted && !w.CustomerEmail.StartsWith("v1:"))
                .Take(BatchSize)
                .ToListAsync(ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
                row.CustomerEmail = encryption.Encrypt(row.CustomerEmail)
                    ?? throw new InvalidOperationException("Encryption returned null for a non-null waitlist customer email.");

            await db.SaveChangesAsync(ct);
            total += rows.Count;
            if (rows.Count < BatchSize) break;
        }
        return total;
    }

    private async Task<int> MigrateStaffEmailsAsync(
        ChronithDbContext db,
        IEncryptionService encryption,
        CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            var rows = await db.StaffMembers
                .IgnoreQueryFilters()
                .Where(s => !s.IsDeleted && !s.Email.StartsWith("v1:"))
                .Take(BatchSize)
                .ToListAsync(ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
                row.Email = encryption.Encrypt(row.Email)
                    ?? throw new InvalidOperationException("Encryption returned null for a non-null staff member email.");

            await db.SaveChangesAsync(ct);
            total += rows.Count;
            if (rows.Count < BatchSize) break;
        }
        return total;
    }

    private async Task<int> MigrateBookingTypeSecretsAsync(
        ChronithDbContext db,
        IEncryptionService encryption,
        CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            var rows = await db.BookingTypes
                .IgnoreQueryFilters()
                .Where(bt => !bt.IsDeleted
                    && bt.CustomerCallbackSecret != null
                    && bt.CustomerCallbackSecret != string.Empty
                    && !bt.CustomerCallbackSecret.StartsWith("v1:"))
                .Take(BatchSize)
                .ToListAsync(ct);

            if (rows.Count == 0) break;

            foreach (var row in rows)
                row.CustomerCallbackSecret = encryption.Encrypt(row.CustomerCallbackSecret) ?? row.CustomerCallbackSecret;

            await db.SaveChangesAsync(ct);
            total += rows.Count;
            if (rows.Count < BatchSize) break;
        }
        return total;
    }
}
