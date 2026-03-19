# Azure Key Vault + Encryption Key Versioning Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Migrate all production secrets to Azure Key Vault, implement versioned AES-256-GCM encryption with online re-encryption rotation, and fix JWT multi-key validation so all keys can be rotated without downtime or data loss.

**Architecture:** App Service Key Vault references resolve secrets transparently — no Azure SDK added to the app, `IConfiguration` usage unchanged. AES ciphertext gains a version prefix (`v1:...`) so `EncryptionService` can hold multiple key versions simultaneously and re-encrypt old rows via a background service. JWT signing keys already support an array; this plan fixes the two internal validation methods that only used `keys[0]`.

**Tech Stack:** Azure CLI, Azure Key Vault (Standard SKU, RBAC mode), Azure App Service Managed Identity, `EncryptionService` (AES-256-GCM), `JwtTokenService` (HMAC-SHA256), `EncryptionKeyRotationService` (BackgroundService pattern), xUnit + FluentAssertions + NSubstitute.

---

## Context

### Three tables with encrypted columns

| DbSet                       | Entity                           | Encrypted column               |
| --------------------------- | -------------------------------- | ------------------------------ |
| `TenantNotificationConfigs` | `TenantNotificationConfigEntity` | `Settings` (JSON blob)         |
| `Webhooks`                  | `WebhookEntity`                  | `Secret` (HMAC signing secret) |
| `TenantPaymentConfigs`      | `TenantPaymentConfigEntity`      | `Settings` (JSON blob)         |

### Ciphertext format change

```
Before (today):  SGVsbG8gV29ybGQ==
After  (v1):     v1:SGVsbG8gV29ybGQ==
```

### Future key rotation procedure (no data loss)

1. Generate new key → store in KV as `security-encryption-key-v2`
2. Add `"v2": "@KV(...)"` to `Security:KeyVersions` App Service setting
3. Set `Security:EncryptionKeyVersion` to `v2`
4. Set `Security:EncryptionRotationSourceVersion` to `v1` → triggers rotation service
5. Restart — new writes use `v2:...`, rotation service migrates `v1:...` rows in background
6. When logs confirm 0 rows remaining: remove `EncryptionRotationSourceVersion`, delete `security-encryption-key-v1` from KV

### JWT rotation procedure (no downtime, 24 h overlap)

1. Generate new key
2. Set `Jwt:SigningKeys` = `["<new-key>", "<old-key>"]`
3. Restart — new tokens signed with new key; old in-flight tokens validated by old key (still in array)
4. After 24 h: remove old key from array
5. Restart again

---

## Task 1 — Generate new JWT signing key and AES v1 encryption key

> These are the values that will be stored in Key Vault. Run locally; keep values in a password manager — never commit to source control.

**Step 1: Generate JWT signing key**

```bash
openssl rand -hex 64
```

Save the output. This is `<NEW_JWT_KEY>`.

**Step 2: Generate AES-256 encryption key**

```bash
openssl rand -base64 32
```

Save the output. This is `<NEW_AES_KEY>`. It must decode to exactly 32 bytes.

---

## Task 2 — Create Azure Key Vault

**Step 1: Create the vault**

```bash
az keyvault create \
  --name kv-chronith \
  --resource-group rg-chronith \
  --location southeastasia \
  --sku standard \
  --enable-rbac-authorization true
```

Expected output: JSON object with `"provisioningState": "Succeeded"`.

**Step 2: Note the vault URI**

```bash
az keyvault show --name kv-chronith --resource-group rg-chronith --query properties.vaultUri -o tsv
```

Expected: `https://kv-chronith.vault.azure.net/`

---

## Task 3 — Enable system-assigned managed identity on chronith-api

**Step 1: Assign identity**

```bash
az webapp identity assign \
  --name chronith-api \
  --resource-group rg-chronith
```

**Step 2: Capture the principal ID**

```bash
az webapp identity show \
  --name chronith-api \
  --resource-group rg-chronith \
  --query principalId -o tsv
```

Save the output. This is `<PRINCIPAL_ID>`.

---

## Task 4 — Grant Key Vault Secrets User role to the managed identity

**Step 1: Get vault resource ID**

```bash
KV_ID=$(az keyvault show --name kv-chronith --resource-group rg-chronith --query id -o tsv)
```

**Step 2: Assign role**

```bash
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee <PRINCIPAL_ID> \
  --scope $KV_ID
```

Expected output: JSON with `"roleDefinitionName": "Key Vault Secrets User"`.

---

## Task 5 — Store the four active secrets in Key Vault

> Also grant yourself **Key Vault Secrets Officer** on the vault so you can write secrets:
>
> ```bash
> MY_ID=$(az ad signed-in-user show --query id -o tsv)
> az role assignment create --role "Key Vault Secrets Officer" --assignee $MY_ID --scope $KV_ID
> ```

**Step 1: JWT signing key**

```bash
az keyvault secret set \
  --vault-name kv-chronith \
  --name jwt-signing-key \
  --value "<NEW_JWT_KEY>"
```

**Step 2: AES encryption key (versioned name)**

```bash
az keyvault secret set \
  --vault-name kv-chronith \
  --name security-encryption-key-v1 \
  --value "<NEW_AES_KEY>"
```

**Step 3: Neon DB connection string**

```bash
az keyvault secret set \
  --vault-name kv-chronith \
  --name db-connection-string \
  --value "Host=<host>;Database=<db>;Username=<user>;Password=<new_neon_password>;SSL Mode=Require"
```

**Step 4: Upstash Redis connection string**

```bash
az keyvault secret set \
  --vault-name kv-chronith \
  --name redis-connection-string \
  --value "<host>:<port>,password=<new_upstash_password>,ssl=True,abortConnect=False"
```

---

## Task 6 — Update EncryptionOptions

**Files:**

- Modify: `src/Chronith.Application/Options/EncryptionOptions.cs`

**Step 1: Replace the file contents**

```csharp
namespace Chronith.Application.Options;

public sealed class EncryptionOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// The version tag used for all new encryptions (e.g. "v1").
    /// Must be a key in <see cref="KeyVersions"/>.
    /// </summary>
    public string EncryptionKeyVersion { get; set; } = "v1";

    /// <summary>
    /// Map of version tag → Base64-encoded 32-byte AES-256-GCM key.
    /// Add new versions here when rotating; keep old versions until re-encryption completes.
    /// </summary>
    public Dictionary<string, string> KeyVersions { get; set; } = [];

    /// <summary>
    /// When set, the <see cref="EncryptionKeyRotationService"/> will migrate
    /// ciphertexts prefixed with this version to <see cref="EncryptionKeyVersion"/>.
    /// Remove this setting after rotation completes.
    /// </summary>
    public string? EncryptionRotationSourceVersion { get; set; }
}
```

**Step 2: Build to confirm no compile errors**

```bash
dotnet build Chronith.slnx -c Release --no-restore 2>&1 | tail -5
```

Expected: `Build succeeded.`

> **Note:** `EncryptionKey` (the old single-key property) is removed. Any remaining references to `options.Value.EncryptionKey` will now be compile errors — they will be fixed in Task 7.

---

## Task 7 — Update EncryptionService: versioned encrypt/decrypt (TDD)

**Files:**

- Modify: `tests/Chronith.Tests.Unit/Infrastructure/Security/EncryptionServiceTests.cs`
- Modify: `src/Chronith.Infrastructure/Security/EncryptionService.cs`

### Step 1: Write failing tests

Replace the content of `EncryptionServiceTests.cs`:

```csharp
using System.Security.Cryptography;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure.Security;

public class EncryptionServiceTests
{
    private static string NewBase64Key()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }

    private static IEncryptionService CreateSut(
        string currentVersion = "v1",
        Dictionary<string, string>? extraVersions = null,
        string? rotationSourceVersion = null)
    {
        var versions = new Dictionary<string, string> { [currentVersion] = NewBase64Key() };
        if (extraVersions is not null)
            foreach (var (k, v) in extraVersions) versions[k] = v;

        var options = Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = currentVersion,
            KeyVersions = versions,
            EncryptionRotationSourceVersion = rotationSourceVersion
        });
        return new EncryptionService(options);
    }

    // ── round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalPlaintext()
    {
        var sut = CreateSut();
        const string plaintext = "smtp-password-secret-value-123!";

        var ciphertext = sut.Encrypt(plaintext);
        var result = sut.Decrypt(ciphertext);

        result.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_SamePlaintextTwice_ProducesDifferentCiphertexts()
    {
        var sut = CreateSut();
        const string plaintext = "same-plaintext-different-nonce";

        var first = sut.Encrypt(plaintext);
        var second = sut.Encrypt(plaintext);

        first.Should().NotBe(second, "each encryption uses a unique random nonce");
    }

    // ── version prefix ────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_ProducesCiphertextWithVersionPrefix()
    {
        var sut = CreateSut(currentVersion: "v1");
        var ciphertext = sut.Encrypt("any-plaintext");
        ciphertext.Should().StartWith("v1:", "ciphertext must carry the key version");
    }

    [Fact]
    public void Encrypt_WithDifferentVersion_UsesCorrectPrefix()
    {
        var sut = CreateSut(currentVersion: "v2");
        var ciphertext = sut.Encrypt("any-plaintext");
        ciphertext.Should().StartWith("v2:");
    }

    // ── multi-version decryption ──────────────────────────────────────────────

    [Fact]
    public void Decrypt_OldVersionCiphertext_DecryptsWithOldKey()
    {
        // Produce a v1 ciphertext
        var v1Key = NewBase64Key();
        var v1Sut = Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v1",
            KeyVersions = new Dictionary<string, string> { ["v1"] = v1Key }
        });
        var v1Service = new EncryptionService(v1Sut);
        var v1Ciphertext = v1Service.Encrypt("secret-data")!;

        // A service with both v1 and v2 should still decrypt the v1 ciphertext
        var v2Key = NewBase64Key();
        var bothOptions = Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v2",
            KeyVersions = new Dictionary<string, string> { ["v1"] = v1Key, ["v2"] = v2Key }
        });
        var bothService = new EncryptionService(bothOptions);

        var result = bothService.Decrypt(v1Ciphertext);
        result.Should().Be("secret-data");
    }

    // ── tamper detection ──────────────────────────────────────────────────────

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        var sut = CreateSut();
        var ciphertext = sut.Encrypt("sensitive-data")!;

        // Strip the version prefix, tamper with the base64 payload, reattach prefix
        var colonIdx = ciphertext.IndexOf(':');
        var version = ciphertext[..colonIdx];
        var bytes = Convert.FromBase64String(ciphertext[(colonIdx + 1)..]);
        bytes[^1] ^= 0xFF;
        var tampered = $"{version}:{Convert.ToBase64String(bytes)}";

        var act = () => sut.Decrypt(tampered);
        act.Should().Throw<CryptographicException>("AES-GCM tag verification must fail on tampered data");
    }

    // ── null / empty passthrough ──────────────────────────────────────────────

    [Fact]
    public void Encrypt_NullInput_ReturnsNull()
    {
        var sut = CreateSut();
        sut.Encrypt(null).Should().BeNull();
    }

    [Fact]
    public void Decrypt_NullInput_ReturnsNull()
    {
        var sut = CreateSut();
        sut.Decrypt(null).Should().BeNull();
    }

    [Fact]
    public void Encrypt_EmptyString_ReturnsEmptyString()
    {
        var sut = CreateSut();
        sut.Encrypt(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Decrypt_EmptyString_ReturnsEmptyString()
    {
        var sut = CreateSut();
        sut.Decrypt(string.Empty).Should().BeEmpty();
    }

    // ── error cases ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenKeyIsNot32Bytes()
    {
        var shortKey = Convert.ToBase64String(new byte[16]);
        var options = Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v1",
            KeyVersions = new Dictionary<string, string> { ["v1"] = shortKey }
        });

        var act = () => new EncryptionService(options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*32 bytes*");
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenCurrentVersionNotInKeyVersions()
    {
        var options = Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v2",
            KeyVersions = new Dictionary<string, string> { ["v1"] = NewBase64Key() }
        });

        var act = () => new EncryptionService(options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*v2*");
    }

    [Fact]
    public void Decrypt_UnknownVersionPrefix_ThrowsInvalidOperationException()
    {
        var sut = CreateSut(currentVersion: "v1");
        // Manually craft a ciphertext with an unknown version
        var act = () => sut.Decrypt("v99:SGVsbG8=");
        act.Should().Throw<InvalidOperationException>().WithMessage("*v99*");
    }

    [Fact]
    public void Decrypt_LegacyUnversionedCiphertext_ThrowsInvalidOperationException()
    {
        // A ciphertext with no "version:" prefix (old format before this change)
        // must fail with a clear error rather than a confusing CryptographicException
        var sut = CreateSut();
        var act = () => sut.Decrypt("SGVsbG8gV29ybGQ=");
        act.Should().Throw<InvalidOperationException>().WithMessage("*version*");
    }
}
```

**Step 2: Run tests — expect failures**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~EncryptionServiceTests" -c Release 2>&1 | tail -15
```

Expected: several test failures (EncryptionService still uses old single-key design).

**Step 3: Rewrite EncryptionService**

Replace `src/Chronith.Infrastructure/Security/EncryptionService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Security;

/// <summary>
/// AES-256-GCM authenticated encryption service with key versioning.
///
/// Ciphertext format: {version}:{base64(nonce[12] || ciphertext[n] || tag[16])}
///
/// Multiple key versions may coexist. <see cref="EncryptionOptions.EncryptionKeyVersion"/>
/// determines which key is used for new encryptions. Decryption inspects the version
/// prefix and selects the matching key, so old ciphertexts remain readable while
/// <see cref="EncryptionKeyRotationService"/> migrates them in the background.
/// </summary>
public sealed class EncryptionService : IEncryptionService
{
    private readonly string _currentVersion;
    private readonly IReadOnlyDictionary<string, byte[]> _keys;

    public EncryptionService(IOptions<EncryptionOptions> options)
    {
        var opts = options.Value;
        _currentVersion = opts.EncryptionKeyVersion;

        if (opts.KeyVersions is not { Count: > 0 })
            throw new InvalidOperationException(
                "EncryptionOptions.KeyVersions must contain at least one entry.");

        if (!opts.KeyVersions.ContainsKey(_currentVersion))
            throw new InvalidOperationException(
                $"EncryptionOptions.EncryptionKeyVersion '{_currentVersion}' " +
                $"is not present in KeyVersions.");

        var keys = new Dictionary<string, byte[]>(opts.KeyVersions.Count);
        foreach (var (version, b64Key) in opts.KeyVersions)
        {
            var keyBytes = Convert.FromBase64String(b64Key);
            if (keyBytes.Length != 32)
                throw new InvalidOperationException(
                    $"Key for version '{version}' must be exactly 32 bytes (256-bit). " +
                    $"Got {keyBytes.Length} bytes.");
            keys[version] = keyBytes;
        }
        _keys = keys;
    }

    public string? Encrypt(string? plaintext)
    {
        if (plaintext is null) return null;
        if (plaintext.Length == 0) return string.Empty;

        var key = _keys[_currentVersion];

        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBytes = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertextBytes, tag);

        // Pack: nonce (12) + ciphertext (n) + tag (16)
        var packed = new byte[nonce.Length + ciphertextBytes.Length + tag.Length];
        nonce.CopyTo(packed, 0);
        ciphertextBytes.CopyTo(packed, nonce.Length);
        tag.CopyTo(packed, nonce.Length + ciphertextBytes.Length);

        return $"{_currentVersion}:{Convert.ToBase64String(packed)}";
    }

    public string? Decrypt(string? encoded)
    {
        if (encoded is null) return null;
        if (encoded.Length == 0) return string.Empty;

        var colonIdx = encoded.IndexOf(':');
        if (colonIdx <= 0)
            throw new InvalidOperationException(
                $"Ciphertext has no version prefix. Expected format: '{{version}}:{{base64}}'. " +
                $"This ciphertext was produced before key versioning was introduced and cannot " +
                $"be decrypted (data loss accepted during initial v1 rotation).");

        var version = encoded[..colonIdx];
        var payload = encoded[(colonIdx + 1)..];

        if (!_keys.TryGetValue(version, out var key))
            throw new InvalidOperationException(
                $"Unknown encryption key version '{version}'. " +
                $"Add this version to Security:KeyVersions configuration.");

        var data = Convert.FromBase64String(payload);
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;  // 12
        var tagSize = AesGcm.TagByteSizes.MaxSize;       // 16

        var nonce = data[..nonceSize];
        var tag = data[^tagSize..];
        var ciphertext = data[nonceSize..^tagSize];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, tagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext); // throws CryptographicException on tamper

        return Encoding.UTF8.GetString(plaintext);
    }
}
```

**Step 4: Run tests — expect all pass**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~EncryptionServiceTests" -c Release 2>&1 | tail -10
```

Expected: all `EncryptionServiceTests` green.

**Step 5: Run full unit test suite to catch regressions**

```bash
dotnet test tests/Chronith.Tests.Unit -c Release 2>&1 | tail -10
```

Expected: same number of tests passing as before (509+), 0 failures.

**Step 6: Commit**

```bash
git add src/Chronith.Application/Options/EncryptionOptions.cs \
        src/Chronith.Infrastructure/Security/EncryptionService.cs \
        tests/Chronith.Tests.Unit/Infrastructure/Security/EncryptionServiceTests.cs
git commit -m "feat(infra): versioned AES-256-GCM encryption with key rotation support"
```

---

## Task 8 — Add EncryptionKeyRotationService (BackgroundService)

**Files:**

- Create: `src/Chronith.Infrastructure/Services/EncryptionKeyRotationService.cs`
- Modify: `src/Chronith.Infrastructure/DependencyInjection.cs`

### Background

This service runs when `Security:EncryptionRotationSourceVersion` is set and differs from `Security:EncryptionKeyVersion`. It scans the three encrypted-column tables for rows whose ciphertext starts with `{sourceVersion}:`, decrypts them with the old key, re-encrypts with the current key, and saves. It then exits.

It uses `IServiceScopeFactory` to obtain a scoped `ChronithDbContext` per iteration (consistent with other background services in the codebase). It calls `IEncryptionService` which already knows all key versions.

**Step 1: Create the service**

Create `src/Chronith.Infrastructure/Services/EncryptionKeyRotationService.cs`:

```csharp
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

                var remaining = await RotateBatchAsync(db, encryption, sourcePrefix, stoppingToken);

                if (remaining == 0)
                {
                    logger.LogInformation(
                        "EncryptionKeyRotationService: rotation from {Source} to {Target} complete. " +
                        "Remove Security:EncryptionRotationSourceVersion and the old Key Vault secret.",
                        sourceVersion, targetVersion);
                    return;
                }

                logger.LogInformation(
                    "EncryptionKeyRotationService: rotated {Count} rows this iteration. Continuing.",
                    remaining);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "EncryptionKeyRotationService: error during rotation iteration.");
            }

            await Task.Delay(IterationDelay, stoppingToken);
        }
    }

    private static async Task<int> RotateBatchAsync(
        ChronithDbContext db,
        IEncryptionService encryption,
        string sourcePrefix,
        CancellationToken ct)
    {
        int total = 0;

        // notification configs
        var notifRows = await db.TenantNotificationConfigs
            .IgnoreQueryFilters()
            .Where(e => !e.IsDeleted && e.Settings.StartsWith(sourcePrefix))
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var row in notifRows)
        {
            var plain = encryption.Decrypt(row.Settings);
            row.Settings = encryption.Encrypt(plain) ?? "{}";
        }
        total += notifRows.Count;

        // webhook secrets
        var webhookRows = await db.Webhooks
            .IgnoreQueryFilters()
            .Where(e => !e.IsDeleted && e.Secret.StartsWith(sourcePrefix))
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var row in webhookRows)
        {
            var plain = encryption.Decrypt(row.Secret);
            row.Secret = encryption.Encrypt(plain) ?? string.Empty;
        }
        total += webhookRows.Count;

        // payment configs
        var paymentRows = await db.TenantPaymentConfigs
            .IgnoreQueryFilters()
            .Where(e => !e.IsDeleted && e.Settings.StartsWith(sourcePrefix))
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var row in paymentRows)
        {
            var plain = encryption.Decrypt(row.Settings);
            row.Settings = encryption.Encrypt(plain) ?? "{}";
        }
        total += paymentRows.Count;

        if (total > 0)
            await db.SaveChangesAsync(ct);

        return total;
    }
}
```

**Step 2: Register in DependencyInjection.cs**

In `src/Chronith.Infrastructure/DependencyInjection.cs`, find the block where other `BackgroundService` types are registered with `AddHostedService` and add:

```csharp
services.AddHostedService<EncryptionKeyRotationService>();
```

**Step 3: Build**

```bash
dotnet build Chronith.slnx -c Release --no-restore 2>&1 | tail -5
```

Expected: `Build succeeded.`

**Step 4: Commit**

```bash
git add src/Chronith.Infrastructure/Services/EncryptionKeyRotationService.cs \
        src/Chronith.Infrastructure/DependencyInjection.cs
git commit -m "feat(infra): add EncryptionKeyRotationService for online key re-encryption"
```

---

## Task 9 — Fix JwtTokenService: multi-key validation for internal methods (TDD)

**Context:** Bearer auth already validates against all configured keys (via `PostConfigure` in `Program.cs:78-86`). The two internal validation methods `ValidateMagicLinkToken` and `ValidateEmailVerificationToken` only use `keys[0]`, so in-flight tokens signed with a secondary key fail immediately after rotation.

**Files:**

- Modify: `tests/Chronith.Tests.Unit/Infrastructure/Auth/JwtTokenServiceMultiKeyTests.cs`
- Modify: `src/Chronith.Infrastructure/Auth/JwtTokenService.cs`

**Step 1: Add failing tests**

Append these test methods to `JwtTokenServiceMultiKeyTests.cs`:

```csharp
[Fact]
public void ValidateMagicLinkToken_SignedWithSecondaryKey_SucceedsWhenBothKeysConfigured()
{
    // Simulate a magic link signed with the OLD key (secondary) during key rotation
    var secondaryKeyBytes = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecondaryKey));
    var creds = new SigningCredentials(secondaryKeyBytes, SecurityAlgorithms.HmacSha256);

    const string tenantSlug = "acme";
    var customerId = Guid.NewGuid();

    var oldToken = new JwtSecurityToken(
        claims:
        [
            new Claim(JwtRegisteredClaimNames.Sub, customerId.ToString()),
            new Claim("email", "customer@example.com"),
            new Claim("tenantSlug", tenantSlug),
            new Claim("purpose", "magic-link-verify"),
        ],
        expires: DateTime.UtcNow.AddHours(24),
        signingCredentials: creds);
    var oldTokenString = new JwtSecurityTokenHandler().WriteToken(oldToken);

    // Service configured with both keys — old token must still validate
    var sut = CreateSut(PrimaryKey, SecondaryKey);

    var result = sut.ValidateMagicLinkToken(oldTokenString, tenantSlug);

    result.Should().Be(customerId,
        "a magic link signed with the secondary (old) key must validate during the rotation window");
}

[Fact]
public void ValidateEmailVerificationToken_SignedWithSecondaryKey_SucceedsWhenBothKeysConfigured()
{
    var secondaryKeyBytes = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecondaryKey));
    var creds = new SigningCredentials(secondaryKeyBytes, SecurityAlgorithms.HmacSha256);

    var userId = Guid.NewGuid();

    var oldToken = new JwtSecurityToken(
        claims:
        [
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("purpose", "email-verify"),
        ],
        expires: DateTime.UtcNow.AddHours(24),
        signingCredentials: creds);
    var oldTokenString = new JwtSecurityTokenHandler().WriteToken(oldToken);

    var sut = CreateSut(PrimaryKey, SecondaryKey);

    var result = sut.ValidateEmailVerificationToken(oldTokenString);

    result.Should().Be(userId,
        "an email verification token signed with the secondary (old) key must validate during rotation");
}
```

**Step 2: Run tests — expect failures**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~JwtTokenServiceMultiKeyTests" -c Release 2>&1 | tail -10
```

Expected: the two new tests fail (`UnauthorizedException` thrown instead of success).

**Step 3: Add a helper to JwtTokenService that builds all configured signing keys**

In `src/Chronith.Infrastructure/Auth/JwtTokenService.cs`, add this private method below `GetPrimarySigningKey()`:

```csharp
/// <summary>
/// Returns all configured signing keys as <see cref="SecurityKey"/> instances,
/// used for token validation so that keys[1], keys[2], etc. remain valid
/// during a rotation window.
/// </summary>
private IEnumerable<SecurityKey> GetAllSigningKeys()
{
    var keys = configuration.GetSection("Jwt:SigningKeys").Get<string[]>();
    if (keys is { Length: > 0 })
        return keys.Select(k => (SecurityKey)new SymmetricSecurityKey(Encoding.UTF8.GetBytes(k)));

    var single = configuration["Jwt:SigningKey"];
    if (!string.IsNullOrEmpty(single))
        return [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(single))];

    return [];
}
```

**Step 4: Update ValidateMagicLinkToken to use all keys**

In `ValidateMagicLinkToken`, replace the `var key = ...` / `IssuerSigningKey = key` lines with `IssuerSigningKeys`:

```csharp
// Remove:
var signingKey = GetPrimarySigningKey();
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

var validationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = key,
    ...
};

// Replace with:
var validationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKeys = GetAllSigningKeys(),
    ...
};
```

**Step 5: Update ValidateEmailVerificationToken the same way**

Apply the identical change in `ValidateEmailVerificationToken`.

**Step 6: Run tests — expect all pass**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~JwtTokenService" -c Release 2>&1 | tail -10
```

Expected: all JWT tests green.

**Step 7: Run full unit suite**

```bash
dotnet test tests/Chronith.Tests.Unit -c Release 2>&1 | tail -10
```

Expected: 0 failures.

**Step 8: Commit**

```bash
git add src/Chronith.Infrastructure/Auth/JwtTokenService.cs \
        tests/Chronith.Tests.Unit/Infrastructure/Auth/JwtTokenServiceMultiKeyTests.cs
git commit -m "fix(infra): validate JWT magic-link and email-verify tokens against all configured keys"
```

---

## Task 10 — Update App Service settings to Key Vault references

> **This task is manual Azure CLI / Azure Portal work. No code changes.**

The App Service setting value format for Key Vault references:

```
@Microsoft.KeyVault(VaultName=kv-chronith;SecretName=<secret-name>)
```

**Step 1: Update the four active secrets**

```bash
az webapp config appsettings set \
  --name chronith-api \
  --resource-group rg-chronith \
  --settings \
    "Jwt__SigningKey=@Microsoft.KeyVault(VaultName=kv-chronith;SecretName=jwt-signing-key)" \
    "Database__ConnectionString=@Microsoft.KeyVault(VaultName=kv-chronith;SecretName=db-connection-string)" \
    "Redis__ConnectionString=@Microsoft.KeyVault(VaultName=kv-chronith;SecretName=redis-connection-string)" \
    "Security__EncryptionKeyVersion=v1" \
    "Security__KeyVersions__v1=@Microsoft.KeyVault(VaultName=kv-chronith;SecretName=security-encryption-key-v1)"
```

**Step 2: Remove the old single-key setting (if it exists)**

```bash
az webapp config appsettings delete \
  --name chronith-api \
  --resource-group rg-chronith \
  --setting-names "Security__EncryptionKey"
```

**Step 3: Update appsettings.json to reflect the new EncryptionOptions shape**

In `src/Chronith.API/appsettings.json`, replace the `Security` section:

```json
"Security": {
  "EncryptionKeyVersion": "SET_VIA_AZURE_APP_SERVICE_OR_ENV",
  "KeyVersions": {
    "v1": "SET_VIA_AZURE_APP_SERVICE_OR_ENV"
  },
  "EncryptionKey": null
}
```

**Step 4: Update Program.cs startup validation**

In `src/Chronith.API/Program.cs`, the startup guard currently checks `Security:EncryptionKey`. Update it to check `Security:EncryptionKeyVersion` and `Security:KeyVersions` instead:

Find the existing guard block (around line 260–270) that checks placeholder values and update the encryption key check:

```csharp
// Remove check for Security:EncryptionKey placeholder
// Add:
var encryptionVersion = app.Configuration["Security:EncryptionKeyVersion"];
var encryptionKey = app.Configuration[$"Security:KeyVersions:{encryptionVersion}"];
if (!isDevelopment && (
    string.IsNullOrEmpty(encryptionVersion) ||
    string.IsNullOrEmpty(encryptionKey) ||
    encryptionKey.StartsWith("SET_VIA_") ||
    encryptionKey == "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="))
    throw new InvalidOperationException(
        "Security:KeyVersions is not configured. Set Security:EncryptionKeyVersion and " +
        "Security:KeyVersions:{version} via Azure App Service settings or Key Vault references.");
```

**Step 5: Build**

```bash
dotnet build Chronith.slnx -c Release --no-restore 2>&1 | tail -5
```

Expected: `Build succeeded.`

**Step 6: Commit**

```bash
git add src/Chronith.API/appsettings.json src/Chronith.API/Program.cs
git commit -m "feat(api): update config shape for versioned encryption keys and KV references"
```

---

## Task 11 — Add CI secrets to GitHub Actions

> **This is a manual GitHub task. No code changes.**

Go to: **GitHub → chronith repo → Settings → Secrets and variables → Actions → New repository secret**

Add two secrets:

| Secret name                  | Value                                                                                 |
| ---------------------------- | ------------------------------------------------------------------------------------- |
| `CI_JWT_SIGNING_KEY`         | Any 32+ char string safe for tests (e.g. `ci-test-jwt-signing-key-not-for-prod-32ch`) |
| `CI_SECURITY_ENCRYPTION_KEY` | A valid base64 32-byte key: run `openssl rand -base64 32`                             |

These are **test-only** values used exclusively in CI test runs. They are not the production KV values.

---

## Task 12 — Verify

**Step 1: Restart App Service to pick up new settings**

```bash
az webapp restart --name chronith-api --resource-group rg-chronith
```

**Step 2: Poll health endpoint**

```bash
for i in $(seq 1 12); do
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" https://chronith-api.azurewebsites.net/health/live)
  echo "[$i] HTTP $STATUS"
  [ "$STATUS" = "200" ] && break
  sleep 10
done
```

Expected: `HTTP 200` within 2 minutes.

**Step 3: Verify Key Vault reference resolution in Azure Portal**

Go to: **Azure Portal → chronith-api → Configuration → Application settings**

Each KV-reference setting should show a green checkmark and status `Resolved`.

If any show `Failed` or `Reference not resolved`, check:

- Managed identity is assigned and has `Key Vault Secrets User` role
- Secret name in the reference matches exactly (case-sensitive)
- Key Vault firewall is not blocking App Service

---

## Task 13 — Final commit and PR

**Step 1: Run full unit test suite one last time**

```bash
dotnet test tests/Chronith.Tests.Unit -c Release 2>&1 | tail -5
```

Expected: 0 failures.

**Step 2: Push branch**

```bash
git push
```

**Step 3: Open PR**

```bash
gh pr create \
  --title "feat(security): Azure Key Vault, versioned AES encryption, key rotation service" \
  --base main \
  --body "..."
```

---

## Post-Rotation Runbook (future reference)

### Rotate AES encryption key (e.g. v1 → v2)

```bash
# 1. Generate new key
openssl rand -base64 32

# 2. Store in KV
az keyvault secret set --vault-name kv-chronith \
  --name security-encryption-key-v2 --value "<new-key>"

# 3. Add v2 to app settings, set as current, set rotation source
az webapp config appsettings set --name chronith-api --resource-group rg-chronith --settings \
  "Security__KeyVersions__v2=@Microsoft.KeyVault(VaultName=kv-chronith;SecretName=security-encryption-key-v2)" \
  "Security__EncryptionKeyVersion=v2" \
  "Security__EncryptionRotationSourceVersion=v1"

# 4. Restart — rotation service begins migrating v1 rows
az webapp restart --name chronith-api --resource-group rg-chronith

# 5. Monitor logs until "Rotation complete"
az webapp log tail --name chronith-api --resource-group rg-chronith

# 6. After completion: remove rotation trigger and old key
az webapp config appsettings delete --name chronith-api --resource-group rg-chronith \
  --setting-names "Security__EncryptionRotationSourceVersion" "Security__KeyVersions__v1"
az keyvault secret delete --vault-name kv-chronith --name security-encryption-key-v1
```

### Rotate JWT signing key (zero-downtime)

```bash
# 1. Generate new key
openssl rand -hex 64

# 2. Add new key as primary, keep old as secondary
az webapp config appsettings set --name chronith-api --resource-group rg-chronith --settings \
  "Jwt__SigningKeys__0=<new-key>" \
  "Jwt__SigningKeys__1=<old-key>"
az webapp restart --name chronith-api --resource-group rg-chronith

# 3. Wait 24h (magic-link + access token TTL)

# 4. Remove old key
az webapp config appsettings set --name chronith-api --resource-group rg-chronith --settings \
  "Jwt__SigningKeys__0=<new-key>"
az webapp config appsettings delete --name chronith-api --resource-group rg-chronith \
  --setting-names "Jwt__SigningKeys__1"
az webapp restart --name chronith-api --resource-group rg-chronith
```
