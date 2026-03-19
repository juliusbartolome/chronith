using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Services;

/// <summary>
/// Migrates encrypted-column rows from an old key version to the current key version.
///
/// Activates only when <see cref="EncryptionOptions.EncryptionRotationSourceVersion"/> is set
/// and differs from <see cref="EncryptionOptions.EncryptionKeyVersion"/>.
///
/// Scans in batches of <see cref="BatchSize"/> rows per table per iteration.
/// Exits cleanly when no old-version rows remain.
///
/// To trigger rotation:
///   1. Add the new key version to Security:KeyVersions
///   2. Set Security:EncryptionKeyVersion = v{new}
///   3. Set Security:EncryptionRotationSourceVersion = v{old}
///   4. Restart the app
///   5. Monitor logs for "Rotation complete"
///   6. Remove Security:EncryptionRotationSourceVersion
///   7. Remove the old key from Security:KeyVersions
///   8. Delete the old secret from Key Vault
/// </summary>
public sealed class EncryptionKeyRotationService(
    IServiceScopeFactory scopeFactory,
    IOptions<EncryptionOptions> options,
    IBackgroundServiceHealthTracker healthTracker,
    ILogger<EncryptionKeyRotationService> logger
) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan IterationDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        var sourceVersion = opts.EncryptionRotationSourceVersion;
        var targetVersion = opts.EncryptionKeyVersion;

        if (string.IsNullOrEmpty(sourceVersion) || sourceVersion == targetVersion)
        {
            logger.LogDebug(
                "EncryptionKeyRotationService: no rotation configured " +
                "(EncryptionRotationSourceVersion is absent or equals EncryptionKeyVersion). Exiting.");
            return;
        }

        logger.LogInformation(
            "EncryptionKeyRotationService: starting rotation from {Source} to {Target}.",
            sourceVersion, targetVersion);

        var sourcePrefix = $"{sourceVersion}:";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ChronithDbContext>();
                var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

                var rotatedCount = await RotateBatchAsync(db, encryption, sourcePrefix, stoppingToken);

                if (rotatedCount == 0)
                {
                    logger.LogInformation(
                        "EncryptionKeyRotationService: rotation from {Source} to {Target} complete. " +
                        "Remove Security:EncryptionRotationSourceVersion and the old Key Vault secret.",
                        sourceVersion, targetVersion);
                    healthTracker.RecordSuccess(nameof(EncryptionKeyRotationService));
                    return;
                }

                logger.LogInformation(
                    "EncryptionKeyRotationService: rotated {Count} rows this iteration. Continuing.",
                    rotatedCount);
                healthTracker.RecordSuccess(nameof(EncryptionKeyRotationService));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "EncryptionKeyRotationService: error during rotation iteration.");
            }

            await Task.Delay(IterationDelay, stoppingToken);
        }
    }

    private async Task<int> RotateBatchAsync(
        ChronithDbContext db,
        IEncryptionService encryption,
        string sourcePrefix,
        CancellationToken ct)
    {
        int total = 0;

        // notification configs (no soft-delete column — IgnoreQueryFilters bypasses tenant filter only)
        var notifRows = await db.TenantNotificationConfigs
            .IgnoreQueryFilters()
            .Where(e => e.Settings.StartsWith(sourcePrefix))
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var row in notifRows)
        {
            try
            {
                var plain = encryption.Decrypt(row.Settings);
                row.Settings = encryption.Encrypt(plain) ?? "{}";
                total++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "EncryptionKeyRotationService: failed to re-encrypt TenantNotificationConfig row {Id}. Skipping.",
                    row.Id);
                db.Entry(row).State = EntityState.Unchanged;
            }
        }

        // webhook secrets
        var webhookRows = await db.Webhooks
            .IgnoreQueryFilters()
            .Where(e => !e.IsDeleted && e.Secret.StartsWith(sourcePrefix))
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var row in webhookRows)
        {
            try
            {
                var plain = encryption.Decrypt(row.Secret);
                row.Secret = encryption.Encrypt(plain) ?? string.Empty;
                total++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "EncryptionKeyRotationService: failed to re-encrypt Webhook row {Id}. Skipping.",
                    row.Id);
                db.Entry(row).State = EntityState.Unchanged;
            }
        }

        // payment configs
        var paymentRows = await db.TenantPaymentConfigs
            .IgnoreQueryFilters()
            .Where(e => !e.IsDeleted && e.Settings.StartsWith(sourcePrefix))
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var row in paymentRows)
        {
            try
            {
                var plain = encryption.Decrypt(row.Settings);
                row.Settings = encryption.Encrypt(plain) ?? "{}";
                total++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "EncryptionKeyRotationService: failed to re-encrypt TenantPaymentConfig row {Id}. Skipping.",
                    row.Id);
                db.Entry(row).State = EntityState.Unchanged;
            }
        }

        if (total > 0)
            await db.SaveChangesAsync(ct);

        return total;
    }
}
