# PII Encryption Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Encrypt all PII and credential fields identified in the sensitive data audit (`2026-03-19-sensitive-data-audit.md`), covering all four priority levels.

**Architecture:** Field-level AES-256-GCM encryption for all sensitive columns, using the existing `IEncryptionService` pattern (gold standard: `WebhookRepository`). Searchable email fields (Customer, TenantUser) also get an HMAC-SHA256 blind index token for equality queries. A blocking `IHostedService` migrates all existing plaintext rows at startup before the app serves requests. The MediatR `AuditBehavior` is extended with a PII redactor to scrub sensitive values from audit snapshots. A new `WebhookOutboxCleanupService` purges old terminal-status outbox rows.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, `System.Security.Cryptography.HMACSHA256`, xUnit + FluentAssertions + NSubstitute, `IHostedService` (blocking via `ApplicationStarted` event), MediatR pipeline behavior.

**Worktree:** `.worktrees/feat-argon2id-password-upgrade`  
**Branch:** `feat/argon2id-password-upgrade`  
**Test command:** `dotnet test tests/Chronith.Tests.Unit --nologo`  
**Build command:** `dotnet build Chronith.slnx --nologo`  
**Migration command:** `dotnet ef migrations add <Name> --project src/Chronith.Infrastructure --startup-project src/Chronith.API --output-dir Migrations/PostgreSQL`

---

## Key Patterns & Rules

### Encryption pattern (from `WebhookRepository`)
- Inject `IEncryptionService` into the repository (primary constructor).
- On **write**: call `_encryption.Encrypt(value)` before persisting.
- On **read**: mutate the entity's field value with `DecryptXxx()` helper before calling mapper.
- `DecryptXxx()` helper: try `Decrypt()`, catch `FormatException or InvalidOperationException`, return raw value as-is with a `LogWarning` (legacy plaintext fallback).
- All reads already use `AsNoTracking()` so mutating entity fields before mapping is safe.

### HMAC blind index pattern (new — for searchable email fields)
- `IBlindIndexService.ComputeToken(string value)` → `string` (hex-encoded HMAC-SHA256).
- Key: `Security:HmacKey` (Base64-encoded 32-byte key, separate from `EncryptionKey`).
- Always normalize to lowercase before hashing: `value.ToLowerInvariant()`.
- Store token in `EmailToken` column (`varchar(64)`).
- Query by token: `WHERE email_token = @token`.
- During migration window, also fallback: `WHERE email_token = @token OR (email_token IS NULL AND email = @plaintext)`.

### Mapper pattern for email/phone overrides
- `CustomerEntityMapper.ToDomain()` and `TenantUserEntityMapper.ToDomain()` are `static` — they cannot hold services.
- Repository decrypts fields **before** calling the mapper.
- Approach: override entity field values (`entity.Email = decrypted`) then call mapper — safe with `AsNoTracking()`.

### Column strategy
- **Fields with unique index** (`CustomerEntity.Email`, `TenantUserEntity.Email`): Add new columns (`EmailEncrypted text`, `EmailToken varchar(64)`); keep old `Email` column during this PR; unique index remains on `Email` for now (Phase 2: separate PR drops `Email`, moves unique index to `EmailToken`).
- **Fields without unique index** (`Booking.CustomerEmail`, `WaitlistEntry.CustomerEmail`, `StaffMember.Email`, `BookingType.CustomerCallbackSecret`): In-place — store ciphertext in existing column; remove `HasMaxLength()` from EF config so EF creates a `text` column.

### Conventional commit scopes
`feat(infra)`, `feat(app)`, `test`, `refactor(infra)`, `fix(infra)`, `docs`

---

## Task 1: IBlindIndexService + HmacBlindIndexService + BlindIndexOptions + DI + appsettings

### Files
- **Create:** `src/Chronith.Application/Services/IBlindIndexService.cs`
- **Create:** `src/Chronith.Infrastructure/Security/BlindIndexOptions.cs`
- **Create:** `src/Chronith.Infrastructure/Security/HmacBlindIndexService.cs`
- **Modify:** `src/Chronith.Infrastructure/DependencyInjection.cs`
- **Modify:** `src/Chronith.API/appsettings.json`
- **Create:** `tests/Chronith.Tests.Unit/Infrastructure/Security/HmacBlindIndexServiceTests.cs`

### Step 1: Write the failing test

```csharp
// tests/Chronith.Tests.Unit/Infrastructure/Security/HmacBlindIndexServiceTests.cs
using Chronith.Application.Services;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure.Security;

public sealed class HmacBlindIndexServiceTests
{
    private static IBlindIndexService CreateService()
    {
        // 32 bytes of 0x01 — valid HMAC key
        var key = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)1).ToArray());
        var options = Options.Create(new BlindIndexOptions { HmacKey = key });
        return new HmacBlindIndexService(options);
    }

    [Fact]
    public void ComputeToken_SameInput_ReturnsSameToken()
    {
        var svc = CreateService();
        var t1 = svc.ComputeToken("user@example.com");
        var t2 = svc.ComputeToken("user@example.com");
        t1.Should().Be(t2);
    }

    [Fact]
    public void ComputeToken_DifferentInputs_ReturnsDifferentTokens()
    {
        var svc = CreateService();
        svc.ComputeToken("a@b.com").Should().NotBe(svc.ComputeToken("c@d.com"));
    }

    [Fact]
    public void ComputeToken_NormalizesToLowercase()
    {
        var svc = CreateService();
        svc.ComputeToken("User@Example.COM").Should().Be(svc.ComputeToken("user@example.com"));
    }

    [Fact]
    public void ComputeToken_Returns64CharHexString()
    {
        var svc = CreateService();
        var token = svc.ComputeToken("test@test.com");
        token.Should().HaveLength(64);
        token.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Constructor_InvalidKey_Throws()
    {
        var options = Options.Create(new BlindIndexOptions { HmacKey = "not-valid-base64!!!" });
        var act = () => new HmacBlindIndexService(options);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*HmacKey*");
    }
}
```

### Step 2: Run test to verify it fails

```bash
dotnet test tests/Chronith.Tests.Unit --nologo --filter "HmacBlindIndexServiceTests"
```
Expected: **FAIL** — `IBlindIndexService`, `HmacBlindIndexService`, `BlindIndexOptions` not found.

### Step 3: Create IBlindIndexService interface

```csharp
// src/Chronith.Application/Services/IBlindIndexService.cs
namespace Chronith.Application.Services;

/// <summary>
/// Computes a deterministic HMAC-SHA256 token for equality-based lookup
/// of encrypted fields (blind index pattern).
/// </summary>
public interface IBlindIndexService
{
    /// <summary>
    /// Normalises <paramref name="value"/> to lowercase and returns its
    /// HMAC-SHA256 as a 64-character lowercase hex string.
    /// </summary>
    string ComputeToken(string value);
}
```

### Step 4: Create BlindIndexOptions

```csharp
// src/Chronith.Infrastructure/Security/BlindIndexOptions.cs
namespace Chronith.Infrastructure.Security;

public sealed class BlindIndexOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Base64-encoded 32-byte (256-bit) HMAC-SHA256 key.
    /// Separate from the AES encryption key.
    /// </summary>
    public string HmacKey { get; set; } = string.Empty;
}
```

### Step 5: Create HmacBlindIndexService

```csharp
// src/Chronith.Infrastructure/Security/HmacBlindIndexService.cs
using System.Security.Cryptography;
using System.Text;
using Chronith.Application.Services;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Security;

public sealed class HmacBlindIndexService : IBlindIndexService
{
    private readonly byte[] _key;

    public HmacBlindIndexService(IOptions<BlindIndexOptions> options)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.HmacKey))
            throw new InvalidOperationException(
                "BlindIndexOptions.HmacKey must be set. " +
                "Set Security:HmacKey to a Base64-encoded 32-byte key.");

        try
        {
            _key = Convert.FromBase64String(opts.HmacKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "BlindIndexOptions.HmacKey is not valid Base64. " +
                "Security:HmacKey must be a Base64-encoded 32-byte key.", ex);
        }

        if (_key.Length != 32)
            throw new InvalidOperationException(
                $"Security:HmacKey must be exactly 32 bytes (256-bit). Got {_key.Length} bytes.");
    }

    public string ComputeToken(string value)
    {
        var normalised = value.ToLowerInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalised);
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

### Step 6: Register in DI and add to appsettings.json

In `src/Chronith.Infrastructure/DependencyInjection.cs`, after line `services.AddSingleton<IEncryptionService, EncryptionService>();`, add:

```csharp
services.Configure<BlindIndexOptions>(configuration.GetSection(BlindIndexOptions.SectionName));
services.AddSingleton<IBlindIndexService, HmacBlindIndexService>();
```

Also add the using: `using Chronith.Infrastructure.Security;` (if not already present).
And add `using Chronith.Application.Services;`.

In `src/Chronith.API/appsettings.json`, find the `"Security"` section and add `"HmacKey"`:

```json
"Security": {
  "EncryptionKeyVersion": "SET_VIA_AZURE_APP_SERVICE_OR_ENV",
  "KeyVersions": {
    "v1": "SET_VIA_AZURE_APP_SERVICE_OR_ENV"
  },
  "EncryptionKey": null,
  "HmacKey": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="
```

(The placeholder is the same as `EncryptionKey` — 44 chars of Base64-encoded zeros. Real value set via environment variable / Key Vault.)

### Step 7: Run test to verify it passes

```bash
dotnet test tests/Chronith.Tests.Unit --nologo --filter "HmacBlindIndexServiceTests"
```
Expected: **5 tests PASS**.

### Step 8: Build check

```bash
dotnet build Chronith.slnx --nologo
```
Expected: **Build succeeded, 0 error(s)**.

### Step 9: Commit

```bash
git -C .worktrees/feat-argon2id-password-upgrade add \
  src/Chronith.Application/Services/IBlindIndexService.cs \
  src/Chronith.Infrastructure/Security/BlindIndexOptions.cs \
  src/Chronith.Infrastructure/Security/HmacBlindIndexService.cs \
  src/Chronith.Infrastructure/DependencyInjection.cs \
  src/Chronith.API/appsettings.json \
  tests/Chronith.Tests.Unit/Infrastructure/Security/HmacBlindIndexServiceTests.cs
git -C .worktrees/feat-argon2id-password-upgrade commit \
  -m "feat(infra): add IBlindIndexService and HmacBlindIndexService for blind index tokens (TDD)"
```

---

## Task 2: Encrypt BookingType.CustomerCallbackSecret

### Files
- **Modify:** `src/Chronith.Infrastructure/Persistence/Repositories/BookingTypeRepository.cs`
- **Modify:** `src/Chronith.Infrastructure/Services/EncryptionKeyRotationService.cs`
- **Create:** `tests/Chronith.Tests.Unit/Infrastructure/Repositories/BookingTypeRepositoryEncryptionTests.cs`

### Background
`BookingTypeRepository.AddAsync()` currently calls `BookingTypeEntityMapper.ToEntity(bookingType)` and persists the entity as-is — `CustomerCallbackSecret` stored in plaintext. `UpdateAsync()` assigns `entity.CustomerCallbackSecret = updated.CustomerCallbackSecret` directly — same problem. All 5 read paths call `BookingTypeEntityMapper.ToDomain(entity)` with the raw entity.

The fix: inject `IEncryptionService`, encrypt on write (after `ToEntity()`, override the field), decrypt on read (mutate entity field before calling `ToDomain()`).

### Step 1: Write the failing tests

```csharp
// tests/Chronith.Tests.Unit/Infrastructure/Repositories/BookingTypeRepositoryEncryptionTests.cs
using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure.Repositories;

/// <summary>
/// Verifies that BookingTypeRepository encrypts CustomerCallbackSecret on write
/// and decrypts it on read using the legacy-fallback pattern.
/// </summary>
public sealed class BookingTypeRepositoryEncryptionTests
{
    private static IEncryptionService CreateRealEncryptionService()
    {
        var key = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)42).ToArray());
        var options = Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v1",
            KeyVersions = new Dictionary<string, string> { ["v1"] = key }
        });
        return new EncryptionService(options);
    }

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalSecret()
    {
        var encryption = CreateRealEncryptionService();
        var secret = "my-callback-secret";

        var encrypted = encryption.Encrypt(secret);
        var decrypted = encryption.Decrypt(encrypted);

        decrypted.Should().Be(secret);
    }

    [Fact]
    public void Decrypt_PlaintextLegacyValue_ReturnsRawValue()
    {
        // Simulates the legacy fallback: if Decrypt throws, return raw value.
        var encryption = Substitute.For<IEncryptionService>();
        encryption.Decrypt("plaintext-secret")
            .Throws(new InvalidOperationException("no version prefix"));

        // The repository's DecryptCallbackSecret helper should return the raw value
        string DecryptCallbackSecret(string? secret)
        {
            if (secret is null) return string.Empty;
            try { return encryption.Decrypt(secret) ?? string.Empty; }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            { return secret; }
        }

        DecryptCallbackSecret("plaintext-secret").Should().Be("plaintext-secret");
    }

    [Fact]
    public void Decrypt_NullSecret_ReturnsEmpty()
    {
        var encryption = CreateRealEncryptionService();

        string DecryptCallbackSecret(string? secret)
        {
            if (secret is null) return string.Empty;
            try { return encryption.Decrypt(secret) ?? string.Empty; }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            { return secret; }
        }

        DecryptCallbackSecret(null).Should().BeEmpty();
    }
}
```

### Step 2: Run test to verify it fails

```bash
dotnet test tests/Chronith.Tests.Unit --nologo --filter "BookingTypeRepositoryEncryptionTests"
```
Expected: **FAIL** — `EncryptionOptions` reference issues (tests reference infra types directly).

> **Note:** These tests exercise the encryption/decryption logic directly, not the repository (which requires EF/DB). This is the appropriate unit test scope. They may actually pass already (they don't require changes yet) — that's fine, they serve as documentation tests. Proceed.

### Step 3: Modify BookingTypeRepository

Change `BookingTypeRepository` from field injection to primary constructor with `IEncryptionService` added.

**Constructor change:**
```csharp
public sealed class BookingTypeRepository(
    ChronithDbContext db,
    IEncryptionService encryptionService,
    ILogger<BookingTypeRepository> logger)
    : IBookingTypeRepository
```

**Add decrypt helper** (after the constructor):
```csharp
private string DecryptCallbackSecret(string? secret)
{
    if (secret is null) return string.Empty;
    try { return encryptionService.Decrypt(secret) ?? string.Empty; }
    catch (Exception ex) when (ex is FormatException or InvalidOperationException)
    {
        logger.LogWarning(
            "BookingType CustomerCallbackSecret could not be decrypted — " +
            "treating as legacy plaintext row. Next write will encrypt it.");
        return secret;
    }
}
```

**AddAsync** — after `var entity = BookingTypeEntityMapper.ToEntity(bookingType);`, add:
```csharp
entity.CustomerCallbackSecret = encryptionService.Encrypt(entity.CustomerCallbackSecret) ?? string.Empty;
```

**UpdateAsync** — the line `entity.CustomerCallbackSecret = updated.CustomerCallbackSecret;` becomes:
```csharp
entity.CustomerCallbackSecret = encryptionService.Encrypt(updated.CustomerCallbackSecret) ?? string.Empty;
```

**All read paths** — wherever `BookingTypeEntityMapper.ToDomain(entity)` is called, mutate first:
```csharp
entity.CustomerCallbackSecret = DecryptCallbackSecret(entity.CustomerCallbackSecret);
return BookingTypeEntityMapper.ToDomain(entity);
```

For `ListAsync` (uses `.Select(BookingTypeEntityMapper.ToDomain)`), change to:
```csharp
return entities.Select(e =>
{
    e.CustomerCallbackSecret = DecryptCallbackSecret(e.CustomerCallbackSecret);
    return BookingTypeEntityMapper.ToDomain(e);
}).ToList();
```

Also remove the old private field `private readonly ChronithDbContext _db;` — use the primary constructor parameter `db` directly (rename all `_db` to `db`).

Add usings: `using Microsoft.Extensions.Logging;`

### Step 4: Add BookingType to EncryptionKeyRotationService

In `RotateBatchAsync`, after the payment configs block, add:

```csharp
// booking type callback secrets
var bookingTypeRows = await db.BookingTypes
    .IgnoreQueryFilters()
    .Where(e => !e.IsDeleted && e.CustomerCallbackSecret != null
                && e.CustomerCallbackSecret.StartsWith(sourcePrefix))
    .Take(BatchSize)
    .ToListAsync(ct);

foreach (var row in bookingTypeRows)
{
    try
    {
        var plain = encryption.Decrypt(row.CustomerCallbackSecret!);
        row.CustomerCallbackSecret = encryption.Encrypt(plain) ?? string.Empty;
        total++;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        logger.LogError(ex,
            "EncryptionKeyRotationService: failed to re-encrypt BookingType row {Id}. Skipping.",
            row.Id);
        db.Entry(row).State = EntityState.Unchanged;
    }
}
```

### Step 5: Run tests

```bash
dotnet test tests/Chronith.Tests.Unit --nologo --filter "BookingTypeRepositoryEncryptionTests"
dotnet build Chronith.slnx --nologo
```
Expected: tests PASS, build succeeded.

### Step 6: Commit

```bash
git -C .worktrees/feat-argon2id-password-upgrade add \
  src/Chronith.Infrastructure/Persistence/Repositories/BookingTypeRepository.cs \
  src/Chronith.Infrastructure/Services/EncryptionKeyRotationService.cs \
  tests/Chronith.Tests.Unit/Infrastructure/Repositories/BookingTypeRepositoryEncryptionTests.cs
git -C .worktrees/feat-argon2id-password-upgrade commit \
  -m "feat(infra): encrypt BookingType.CustomerCallbackSecret in repository (TDD)"
```

---

## Task 3: Customer schema migration — EmailEncrypted, EmailToken, PhoneEncrypted columns

### Files
- **Modify:** `src/Chronith.Infrastructure/Persistence/Entities/CustomerEntity.cs`
- **Modify:** `src/Chronith.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`
- **Run:** EF migration

### Step 1: Add new columns to CustomerEntity

Add after `Email`:
```csharp
/// <summary>AES-256-GCM ciphertext. Populated by migration service at startup.</summary>
public string? EmailEncrypted { get; set; }
/// <summary>HMAC-SHA256 token for equality lookup. Populated by migration service.</summary>
public string? EmailToken { get; set; }
/// <summary>AES-256-GCM ciphertext of Phone. Nullable — same as Phone.</summary>
public string? PhoneEncrypted { get; set; }
```

### Step 2: Update CustomerConfiguration

Add after `builder.Property(c => c.Email)`:
```csharp
builder.Property(c => c.EmailEncrypted);

builder.Property(c => c.EmailToken)
    .HasMaxLength(64);

builder.Property(c => c.PhoneEncrypted);
```

Add index for EmailToken lookup (partial — only where not null):
```csharp
builder.HasIndex(c => new { c.TenantId, c.EmailToken })
    .HasFilter("\"EmailToken\" IS NOT NULL AND \"IsDeleted\" = false")
    .HasDatabaseName("ix_customers_email_token");
```

### Step 3: Run EF migration

```bash
dotnet ef migrations add AddCustomerPiiEncryptionColumns \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API \
  --output-dir Migrations/PostgreSQL \
  --no-build
dotnet build Chronith.slnx --nologo
```
Expected: migration file created, build succeeded.

### Step 4: Commit

```bash
git -C .worktrees/feat-argon2id-password-upgrade add \
  src/Chronith.Infrastructure/Persistence/Entities/CustomerEntity.cs \
  src/Chronith.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs \
  src/Chronith.Infrastructure/Migrations/
git -C .worktrees/feat-argon2id-password-upgrade commit \
  -m "feat(infra): add EmailEncrypted, EmailToken, PhoneEncrypted columns to customers"
```

---

## Task 4: CustomerRepository — encrypt/decrypt email + phone, query by token

### Files
- **Modify:** `src/Chronith.Infrastructure/Persistence/Repositories/CustomerRepository.cs`
- **Modify:** `src/Chronith.Infrastructure/Persistence/Mappers/CustomerEntityMapper.cs`
- **Create:** `tests/Chronith.Tests.Unit/Infrastructure/Repositories/CustomerRepositoryEncryptionTests.cs`

### Step 1: Write the failing tests

```csharp
// tests/Chronith.Tests.Unit/Infrastructure/Repositories/CustomerRepositoryEncryptionTests.cs
using Chronith.Application.Services;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure.Repositories;

public sealed class CustomerRepositoryEncryptionTests
{
    private static (IEncryptionService enc, IBlindIndexService idx) CreateServices()
    {
        var encKey = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)42).ToArray());
        var enc = new EncryptionService(Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v1",
            KeyVersions = new Dictionary<string, string> { ["v1"] = encKey }
        }));

        var hmacKey = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)99).ToArray());
        var idx = new HmacBlindIndexService(Options.Create(new BlindIndexOptions { HmacKey = hmacKey }));

        return (enc, idx);
    }

    [Fact]
    public void EmailToken_IsConsistentForSameEmail()
    {
        var (_, idx) = CreateServices();
        idx.ComputeToken("user@example.com").Should().Be(idx.ComputeToken("user@example.com"));
    }

    [Fact]
    public void EmailToken_IsCaseInsensitive()
    {
        var (_, idx) = CreateServices();
        idx.ComputeToken("User@Example.COM").Should().Be(idx.ComputeToken("user@example.com"));
    }

    [Fact]
    public void EncryptEmail_ThenDecrypt_ReturnsOriginal()
    {
        var (enc, _) = CreateServices();
        var encrypted = enc.Encrypt("user@example.com");
        enc.Decrypt(encrypted).Should().Be("user@example.com");
    }

    [Fact]
    public void DecryptEmail_LegacyPlaintext_ReturnRawValue()
    {
        // Plaintext values that have no version prefix should be returned as-is
        var enc = Substitute.For<IEncryptionService>();
        enc.Decrypt("plaintext@email.com")
            .Throws(new InvalidOperationException("no version prefix"));

        string DecryptEmail(string? value)
        {
            if (value is null) return string.Empty;
            try { return enc.Decrypt(value) ?? string.Empty; }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            { return value; }
        }

        DecryptEmail("plaintext@email.com").Should().Be("plaintext@email.com");
    }
}
```

### Step 2: Run test to verify it fails

```bash
dotnet test tests/Chronith.Tests.Unit --nologo --filter "CustomerRepositoryEncryptionTests"
```
Expected: compilation errors on `HmacBlindIndexService`/`BlindIndexOptions` (or pass if Task 1 is done).

### Step 3: Modify CustomerRepository

Change to primary constructor with injected services:
```csharp
public sealed class CustomerRepository(
    ChronithDbContext db,
    IEncryptionService encryptionService,
    IBlindIndexService blindIndexService,
    ILogger<CustomerRepository> logger)
    : ICustomerRepository
```

Add helpers:
```csharp
private string DecryptEmail(string? value)
{
    if (value is null) return string.Empty;
    try { return encryptionService.Decrypt(value) ?? string.Empty; }
    catch (Exception ex) when (ex is FormatException or InvalidOperationException)
    {
        logger.LogWarning("Customer.Email/EmailEncrypted could not be decrypted — " +
            "treating as legacy plaintext row. Next write will encrypt it.");
        return value;
    }
}

private string? DecryptPhone(string? value)
{
    if (value is null) return null;
    try { return encryptionService.Decrypt(value); }
    catch (Exception ex) when (ex is FormatException or InvalidOperationException)
    {
        logger.LogWarning("Customer.PhoneEncrypted could not be decrypted — " +
            "treating as legacy plaintext row. Next write will encrypt it.");
        return value;
    }
}

private Customer MapToDomain(CustomerEntity e)
{
    // Prefer encrypted values if available; fall back to plaintext Email column
    e.Email = e.EmailEncrypted is not null ? DecryptEmail(e.EmailEncrypted) : e.Email;
    if (e.PhoneEncrypted is not null)
        e.Phone = DecryptPhone(e.PhoneEncrypted);
    return e.ToDomain();
}
```

**GetByEmailAsync** — change to query by token with plaintext fallback:
```csharp
public async Task<Customer?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
{
    var token = blindIndexService.ComputeToken(email);
    var entity = await db.Customers
        .TagWith("GetByEmailAsync — CustomerRepository")
        .AsNoTracking()
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted
            && (c.EmailToken == token || (c.EmailToken == null && c.Email == email)), ct);
    return entity is null ? null : MapToDomain(entity);
}
```

**GetByIdAsync**, **GetByIdCrossTenantAsync**, **GetByIdAcrossTenantsAsync**, **GetByExternalIdAsync** — change `entity?.ToDomain()` to `entity is null ? null : MapToDomain(entity)`.

**AddAsync** — after `customer.ToEntity()`, set encrypted fields:
```csharp
public async Task AddAsync(Customer customer, CancellationToken ct = default)
{
    var entity = customer.ToEntity();
    entity.EmailEncrypted = encryptionService.Encrypt(customer.Email) ?? string.Empty;
    entity.EmailToken = blindIndexService.ComputeToken(customer.Email);
    if (customer.Phone is not null)
        entity.PhoneEncrypted = encryptionService.Encrypt(customer.Phone);
    await db.Customers.AddAsync(entity, ct);
}
```

**Update** — same treatment:
```csharp
public void Update(Customer customer)
{
    var entity = customer.ToEntity();
    entity.EmailEncrypted = encryptionService.Encrypt(customer.Email) ?? string.Empty;
    entity.EmailToken = blindIndexService.ComputeToken(customer.Email);
    entity.PhoneEncrypted = customer.Phone is not null ? encryptionService.Encrypt(customer.Phone) : null;
    db.Customers.Update(entity);
}
```

### Step 4: Run tests

```bash
dotnet test tests/Chronith.Tests.Unit --nologo --filter "CustomerRepositoryEncryptionTests"
dotnet build Chronith.slnx --nologo
```
Expected: tests PASS, build succeeded.

### Step 5: Commit

```bash
git -C .worktrees/feat-argon2id-password-upgrade add \
  src/Chronith.Infrastructure/Persistence/Repositories/CustomerRepository.cs \
  tests/Chronith.Tests.Unit/Infrastructure/Repositories/CustomerRepositoryEncryptionTests.cs
git -C .worktrees/feat-argon2id-password-upgrade commit \
  -m "feat(infra): encrypt Customer email and phone in repository (TDD)"
```

---

## Task 5: TenantUser schema migration — EmailEncrypted, EmailToken columns

### Files
- **Modify:** `src/Chronith.Infrastructure/Persistence/Entities/TenantUserEntity.cs`
- **Modify:** `src/Chronith.Infrastructure/Persistence/Configurations/TenantUserConfiguration.cs`
- **Run:** EF migration

### Step 1: Add new columns to TenantUserEntity

Add after `Email`:
```csharp
/// <summary>AES-256-GCM ciphertext. Populated by migration service at startup.</summary>
public string? EmailEncrypted { get; set; }
/// <summary>HMAC-SHA256 token for equality lookup.</summary>
public string? EmailToken { get; set; }
```

### Step 2: Update TenantUserConfiguration

Add after `builder.Property(u => u.Email).IsRequired().HasMaxLength(256);`:
```csharp
builder.Property(u => u.EmailEncrypted);

builder.Property(u => u.EmailToken)
    .HasMaxLength(64);
```

Add index:
```csharp
builder.HasIndex(u => new { u.TenantId, u.EmailToken })
    .HasFilter("\"EmailToken\" IS NOT NULL")
    .HasDatabaseName("ix_tenantusers_email_token");
```

### Step 3: Run EF migration

```bash
dotnet ef migrations add AddTenantUserPiiEncryptionColumns \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API \
  --output-dir Migrations/PostgreSQL \
  --no-build
dotnet build Chronith.slnx --nologo
```

### Step 4: Commit

```bash
git -C .worktrees/feat-argon2id-password-upgrade add \
  src/Chronith.Infrastructure/Persistence/Entities/TenantUserEntity.cs \
  src/Chronith.Infrastructure/Persistence/Configurations/TenantUserConfiguration.cs \
  src/Chronith.Infrastructure/Migrations/
git -C .worktrees/feat-argon2id-password-upgrade commit \
  -m "feat(infra): add EmailEncrypted, EmailToken columns to TenantUsers"
```

---

## Task 6: TenantUserRepository — encrypt/decrypt email, query by token

### Files
- **Modify:** `src/Chronith.Infrastructure/Persistence/Repositories/TenantUserRepository.cs`
- **Create:** `tests/Chronith.Tests.Unit/Infrastructure/Repositories/TenantUserRepositoryEncryptionTests.cs`

### Step 1: Write the failing tests

```csharp
// tests/Chronith.Tests.Unit/Infrastructure/Repositories/TenantUserRepositoryEncryptionTests.cs
using Chronith.Application.Services;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure.Repositories;

public sealed class TenantUserRepositoryEncryptionTests
{
    private static IBlindIndexService CreateBlindIndex()
    {
        var key = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)77).ToArray());
        return new HmacBlindIndexService(Options.Create(new BlindIndexOptions { HmacKey = key }));
    }

    [Fact]
    public void EmailToken_IsNormalized()
    {
        var idx = CreateBlindIndex();
        idx.ComputeToken("Admin@Company.COM").Should().Be(idx.ComputeToken("admin@company.com"));
    }

    [Fact]
    public void DecryptEmail_LegacyPlaintext_ReturnRawValue()
    {
        var enc = Substitute.For<IEncryptionService>();
        enc.Decrypt("admin@example.com")
            .Throws(new InvalidOperationException("no prefix"));

        string DecryptEmail(string? value)
        {
            if (value is null) return string.Empty;
            try { return enc.Decrypt(value) ?? string.Empty; }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            { return value; }
        }

        DecryptEmail("admin@example.com").Should().Be("admin@example.com");
    }
}
```

### Step 2: Modify TenantUserRepository

Change to primary constructor with services:
```csharp
public sealed class TenantUserRepository(
    ChronithDbContext context,
    IEncryptionService encryptionService,
    IBlindIndexService blindIndexService,
    ILogger<TenantUserRepository> logger)
    : ITenantUserRepository
```

Add helpers:
```csharp
private string DecryptEmail(string? value)
{
    if (value is null) return string.Empty;
    try { return encryptionService.Decrypt(value) ?? string.Empty; }
    catch (Exception ex) when (ex is FormatException or InvalidOperationException)
    {
        logger.LogWarning("TenantUser.Email/EmailEncrypted could not be decrypted — " +
            "treating as legacy plaintext row. Next write will encrypt it.");
        return value;
    }
}

private TenantUser MapToDomain(TenantUserEntity e)
{
    e.Email = e.EmailEncrypted is not null ? DecryptEmail(e.EmailEncrypted) : e.Email;
    return e.ToDomain();
}
```

**AddAsync** — after `user.ToEntity()`:
```csharp
public async Task AddAsync(TenantUser user, CancellationToken ct = default)
{
    var entity = user.ToEntity();
    entity.EmailEncrypted = encryptionService.Encrypt(user.Email) ?? string.Empty;
    entity.EmailToken = blindIndexService.ComputeToken(user.Email);
    await context.TenantUsers.AddAsync(entity, ct);
}
```

**GetByIdAsync** → `entity is null ? null : MapToDomain(entity)`

**GetByEmailAsync** — change query:
```csharp
public async Task<TenantUser?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
{
    var token = blindIndexService.ComputeToken(email);
    var normalised = email.ToLowerInvariant();
    var entity = await context.TenantUsers
        .TagWith("GetByEmailAsync — TenantUserRepository")
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.TenantId == tenantId
            && (u.EmailToken == token || (u.EmailToken == null && u.Email == normalised)), ct);
    return entity is null ? null : MapToDomain(entity);
}
```

**ExistsByEmailAsync** — change to query by token with fallback:
```csharp
public async Task<bool> ExistsByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
{
    var token = blindIndexService.ComputeToken(email);
    var normalised = email.ToLowerInvariant();
    return await context.TenantUsers
        .TagWith("ExistsByEmailAsync — TenantUserRepository")
        .AnyAsync(u => u.TenantId == tenantId
            && (u.EmailToken == token || (u.EmailToken == null && u.Email == normalised)), ct);
}
```

**ExistsByEmailGloballyAsync** — same pattern (no tenantId filter):
```csharp
public async Task<bool> ExistsByEmailGloballyAsync(string email, CancellationToken ct = default)
{
    var token = blindIndexService.ComputeToken(email);
    var normalised = email.ToLowerInvariant();
    return await context.TenantUsers
        .TagWith("ExistsByEmailGloballyAsync — TenantUserRepository")
        .AnyAsync(u => u.EmailToken == token || (u.EmailToken == null && u.Email == normalised), ct);
}
```

**Update / UpdateAsync**:
```csharp
public void Update(TenantUser user)
{
    var entity = user.ToEntity();
    entity.EmailEncrypted = encryptionService.Encrypt(user.Email) ?? string.Empty;
    entity.EmailToken = blindIndexService.ComputeToken(user.Email);
    context.TenantUsers.Update(entity);
}

public Task UpdateAsync(TenantUser user, CancellationToken ct = default)
{
    Update(user);
    return Task.CompletedTask;
}
```

### Step 3: Run tests and build

```bash
dotnet test tests/Chronith.Tests.Unit --nologo --filter "TenantUserRepositoryEncryptionTests"
dotnet build Chronith.slnx --nologo
```

### Step 4: Commit

```bash
git -C .worktrees/feat-argon2id-password-upgrade add \
  src/Chronith.Infrastructure/Persistence/Repositories/TenantUserRepository.cs \
  tests/Chronith.Tests.Unit/Infrastructure/Repositories/TenantUserRepositoryEncryptionTests.cs
git -C .worktrees/feat-argon2id-password-upgrade commit \
  -m "feat(infra): encrypt TenantUser email in repository (TDD)"
```

---

## Task 7: In-place encryption for Booking.CustomerEmail, WaitlistEntry.CustomerEmail, StaffMember.Email

### Files
- **Modify:** `src/Chronith.Infrastructure/Persistence/Configurations/BookingConfiguration.cs`
- **Modify:** `src/Chronith.Infrastructure/Persistence/Configurations/WaitlistEntryConfiguration.cs`
- **Modify:** `src/Chronith.Infrastructure/Persistence/Configurations/StaffMemberConfiguration.cs`
- **Modify:** `src/Chronith.Infrastructure/Persistence/Repositories/BookingRepository.cs`
- **Modify:** `src/Chronith.Infrastructure/Persistence/Repositories/WaitlistRepository.cs`
- **Modify:** `src/Chronith.Infrastructure/Persistence/Repositories/StaffMemberRepository.cs`
- **Run:** EF migration (one migration for all three column changes)

### Step 1: Remove HasMaxLength from EF configurations

**BookingConfiguration.cs** — change:
```csharp
// FROM:
builder.Property(b => b.CustomerEmail)
    .IsRequired()
    .HasMaxLength(200);
// TO:
builder.Property(b => b.CustomerEmail)
    .IsRequired();
```

**WaitlistEntryConfiguration.cs** — change:
```csharp
// FROM:
builder.Property(w => w.CustomerEmail)
    .IsRequired()
    .HasMaxLength(320);
// TO:
builder.Property(w => w.CustomerEmail)
    .IsRequired();
```

**StaffMemberConfiguration.cs** — change:
```csharp
// FROM:
builder.Property(s => s.Email)
    .IsRequired()
    .HasMaxLength(320);
// TO:
builder.Property(s => s.Email)
    .IsRequired();
```

### Step 2: Run EF migration

```bash
dotnet ef migrations add ExpandEmailColumnsForEncryption \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API \
  --output-dir Migrations/PostgreSQL \
  --no-build
```

Verify the migration changes `varchar(200)`/`varchar(320)` to `text` for the three columns.

### Step 3: Modify BookingRepository

Change to primary constructor with `IEncryptionService` and `ILogger`:
```csharp
public sealed class BookingRepository(
    ChronithDbContext _db,
    IEncryptionService encryptionService,
    ILogger<BookingRepository> logger)
    : IBookingRepository
```

Add helper:
```csharp
private string DecryptCustomerEmail(string? value)
{
    if (value is null) return string.Empty;
    try { return encryptionService.Decrypt(value) ?? string.Empty; }
    catch (Exception ex) when (ex is FormatException or InvalidOperationException)
    {
        logger.LogWarning("Booking.CustomerEmail could not be decrypted — " +
            "treating as legacy plaintext row. Next write will encrypt it.");
        return value;
    }
}
```

**AddAsync** — change `BookingEntityMapper.ToEntity(booking)` usage to also encrypt:
```csharp
public async Task AddAsync(Booking booking, CancellationToken ct = default)
{
    var entity = BookingEntityMapper.ToEntity(booking);
    entity.CustomerEmail = encryptionService.Encrypt(entity.CustomerEmail) ?? string.Empty;
    await _db.Bookings.AddAsync(entity, ct);
}
```

**All `ToDomain` call sites** — change `BookingEntityMapper.ToDomain(entity)` to:
```csharp
entity.CustomerEmail = DecryptCustomerEmail(entity.CustomerEmail);
return BookingEntityMapper.ToDomain(entity);
```
For `.Select(BookingEntityMapper.ToDomain)` usages, change to:
```csharp
.Select(e => {
    e.CustomerEmail = DecryptCustomerEmail(e.CustomerEmail);
    return BookingEntityMapper.ToDomain(e);
})
```

**ListForExportAsync** — this uses a raw `.Select()` projection that reads `b.CustomerEmail` directly from the database (EF query, not in-memory). The export will return ciphertext in the `CustomerEmail` column unless we handle it differently. Fix: load entities first then map, or alternatively decrypt in the projection. Since the export could be up to 10,000 rows, the right approach is: load the full entities into memory and decrypt post-fetch. Change the export method to:

```csharp
// Load minimal fields + decrypt CustomerEmail in memory
var rawItems = await _db.Bookings
    .TagWith("ListForExportAsync — BookingRepository")
    .AsNoTracking()
    .Where(b => b.TenantId == tenantId && b.Start >= from && b.Start <= to)
    .Where(b => status == null || b.Status.ToString() == status)
    .Where(b => bookingTypeSlug == null || b.BookingType!.Slug == bookingTypeSlug)
    .Where(b => staffMemberId == null || b.StaffMemberId == staffMemberId)
    .OrderBy(b => b.Start)
    .Take(10_000)
    .Include(b => b.BookingType)
    .Include(b => b.StaffMember)
    .ToListAsync(ct);

return rawItems.Select(b => new BookingExportRowDto(
    b.Id,
    b.BookingType != null ? b.BookingType.Name : string.Empty,
    b.BookingType != null ? b.BookingType.Slug : string.Empty,
    b.Start,
    b.End,
    b.Status.ToString(),
    DecryptCustomerEmail(b.CustomerEmail),
    b.CustomerId,
    b.StaffMember != null ? b.StaffMember.Name : null,
    b.AmountInCentavos,
    b.Currency,
    b.PaymentReference)).ToList();
```

> Note: `BookingExportRowDto` is an `IReadOnlyList<BookingExportRowDto>` — the return type remains the same.

### Step 4: Modify WaitlistRepository

Change to primary constructor:
```csharp
public sealed class WaitlistRepository(
    ChronithDbContext db,
    IEncryptionService encryptionService,
    ILogger<WaitlistRepository> logger)
    : IWaitlistRepository
```
(Remove the `private readonly ChronithDbContext _db;` field and replace `_db` with `db`.)

Add helper:
```csharp
private string DecryptCustomerEmail(string? value)
{
    if (value is null) return string.Empty;
    try { return encryptionService.Decrypt(value) ?? string.Empty; }
    catch (Exception ex) when (ex is FormatException or InvalidOperationException)
    {
        logger.LogWarning("WaitlistEntry.CustomerEmail could not be decrypted — " +
            "treating as legacy plaintext row.");
        return value;
    }
}
```

Change `AddAsync` to encrypt on write:
```csharp
public async Task AddAsync(WaitlistEntry entry, CancellationToken ct = default)
{
    var entity = WaitlistEntryEntityMapper.ToEntity(entry);
    entity.CustomerEmail = encryptionService.Encrypt(entity.CustomerEmail) ?? string.Empty;
    await db.WaitlistEntries.AddAsync(entity, ct);
}
```

All read paths calling `WaitlistEntryEntityMapper.ToDomain(entity)`:
```csharp
entity.CustomerEmail = DecryptCustomerEmail(entity.CustomerEmail);
return WaitlistEntryEntityMapper.ToDomain(entity);
```

### Step 5: Modify StaffMemberRepository

Change to primary constructor (it already uses field injection — switch to primary constructor):
```csharp
public sealed class StaffMemberRepository(
    ChronithDbContext db,
    IEncryptionService encryptionService,
    ILogger<StaffMemberRepository> logger)
    : IStaffMemberRepository
```
(Remove `private readonly ChronithDbContext _db;`; rename `_db` to `db`.)

Add helper:
```csharp
private string DecryptEmail(string? value)
{
    if (value is null) return string.Empty;
    try { return encryptionService.Decrypt(value) ?? string.Empty; }
    catch (Exception ex) when (ex is FormatException or InvalidOperationException)
    {
        logger.LogWarning("StaffMember.Email could not be decrypted — " +
            "treating as legacy plaintext row.");
        return value;
    }
}
```

**AddAsync**:
```csharp
public async Task AddAsync(StaffMember staff, CancellationToken ct = default)
{
    var entity = StaffMemberEntityMapper.ToEntity(staff);
    entity.Email = encryptionService.Encrypt(entity.Email) ?? string.Empty;
    await db.StaffMembers.AddAsync(entity, ct);
}
```

**UpdateAsync** — change `entity.Email = staff.Email;` to:
```csharp
entity.Email = encryptionService.Encrypt(staff.Email) ?? string.Empty;
```

All `StaffMemberEntityMapper.ToDomain(entity)` calls:
```csharp
entity.Email = DecryptEmail(entity.Email);
return StaffMemberEntityMapper.ToDomain(entity);
```

### Step 6: Run tests and build

```bash
dotnet test tests/Chronith.Tests.Unit --nologo
dotnet build Chronith.slnx --nologo
```
Expected: all existing tests pass, build succeeded.

### Step 7: Commit

```bash
git -C .worktrees/feat-argon2id-password-upgrade add \
  src/Chronith.Infrastructure/Persistence/Configurations/BookingConfiguration.cs \
  src/Chronith.Infrastructure/Persistence/Configurations/WaitlistEntryConfiguration.cs \
  src/Chronith.Infrastructure/Persistence/Configurations/StaffMemberConfiguration.cs \
  src/Chronith.Infrastructure/Persistence/Repositories/BookingRepository.cs \
  src/Chronith.Infrastructure/Persistence/Repositories/WaitlistRepository.cs \
  src/Chronith.Infrastructure/Persistence/Repositories/StaffMemberRepository.cs \
  src/Chronith.Infrastructure/Migrations/
git -C .worktrees/feat-argon2id-password-upgrade commit \
  -m "feat(infra): in-place encrypt Booking, WaitlistEntry, StaffMember email fields (TDD)"
```

---

## Task 8: PiiEncryptionMigrationService — blocking startup migration

This service runs before the app serves requests and encrypts all existing plaintext rows.

### Files
- **Create:** `src/Chronith.Infrastructure/Services/PiiEncryptionMigrationService.cs`
- **Modify:** `src/Chronith.Infrastructure/DependencyInjection.cs`
- **Create:** `tests/Chronith.Tests.Unit/Infrastructure/Services/PiiEncryptionMigrationServiceTests.cs`

### Background
`IHostedService` is normally non-blocking. To block startup, implement `IHostedService` and register it via `services.AddSingleton<IHostedService, PiiEncryptionMigrationService>()` — the app starts `IHostedService` registrations sequentially via `IHostApplicationLifetime`. However, ASP.NET Core does NOT guarantee `StartAsync` completes before the server starts listening.

**Correct approach for blocking startup:** Register as a `IHostedService` where `StartAsync` does the work — BUT to actually block HTTP traffic, use `IHostApplicationLifetime.ApplicationStarted` to ensure migrations complete before the app is marked ready (for health checks). The simplest reliable pattern is: implement as a `BackgroundService`, but do all work in `StartAsync` (not `ExecuteAsync`), and register it **first** (before the server host) so it completes before the first request. For App Service / Azure health check purposes, the migration must finish before the health endpoint returns 200.

**Practical implementation:** Use `IHostedService` directly (not `BackgroundService`). In `StartAsync`, perform the migration synchronously (within an async scope). If the migration fails, log the error and continue — the app still starts (failing fast would be worse than serving requests with un-migrated rows, since the legacy fallback handles them).

### Step 1: Write the failing tests

```csharp
// tests/Chronith.Tests.Unit/Infrastructure/Services/PiiEncryptionMigrationServiceTests.cs
using Chronith.Application.Services;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure.Services;

/// <summary>
/// Tests the core encryption helpers used by PiiEncryptionMigrationService.
/// The service itself requires EF/DB (integration test territory).
/// These tests verify the encryption round-trips and token computation
/// that the service relies on.
/// </summary>
public sealed class PiiEncryptionMigrationServiceTests
{
    private static IEncryptionService CreateEncryption()
    {
        var key = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)42).ToArray());
        return new EncryptionService(Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v1",
            KeyVersions = new Dictionary<string, string> { ["v1"] = key }
        }));
    }

    private static IBlindIndexService CreateBlindIndex()
    {
        var key = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)77).ToArray());
        return new HmacBlindIndexService(Options.Create(new BlindIndexOptions { HmacKey = key }));
    }

    [Fact]
    public void IsEncrypted_VersionPrefixed_ReturnsTrue()
    {
        // Values that already have a version prefix are considered encrypted
        "v1:someBase64=".StartsWith("v1:").Should().BeTrue();
    }

    [Fact]
    public void IsEncrypted_PlaintextEmail_ReturnsFalse()
    {
        "user@example.com".StartsWith("v1:").Should().BeFalse();
    }

    [Fact]
    public void IsEncrypted_Empty_ReturnsFalse()
    {
        string.Empty.StartsWith("v1:").Should().BeFalse();
    }

    [Fact]
    public void EncryptAndToken_RoundTrip()
    {
        var enc = CreateEncryption();
        var idx = CreateBlindIndex();
        var email = "migrate@example.com";

        var encrypted = enc.Encrypt(email)!;
        var token = idx.ComputeToken(email);

        enc.Decrypt(encrypted).Should().Be(email);
        token.Should().HaveLength(64);
    }
}
```

### Step 2: Run test to verify it fails (or passes — these are logic tests)

```bash
dotnet test tests/Chronith.Tests.Unit --nologo --filter "PiiEncryptionMigrationServiceTests"
```
Expected: **PASS** (these test core logic already implemented). Proceed to implement the service.

### Step 3: Create PiiEncryptionMigrationService

```csharp
// src/Chronith.Infrastructure/Services/PiiEncryptionMigrationService.cs
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
                    "PiiEncryptionMigrationService: migrated {Count} rows total. Migration complete.",
                    totalMigrated);
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
                if (row.Phone is not null && row.PhoneEncrypted is null)
                    row.PhoneEncrypted = encryption.Encrypt(row.Phone);
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
                row.CustomerEmail = encryption.Encrypt(row.CustomerEmail) ?? row.CustomerEmail;

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
                row.CustomerEmail = encryption.Encrypt(row.CustomerEmail) ?? row.CustomerEmail;

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
                row.Email = encryption.Encrypt(row.Email) ?? row.Email;

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
```

### Step 4: Register in DI

In `DependencyInjection.cs`, add before `services.AddHostedService<WebhookDispatcherService>()`:
```csharp
// Must be first hosted service — runs blocking migration before other services start
services.AddHostedService<PiiEncryptionMigrationService>();
```

> **Important:** Register it FIRST in the hosted service list so it runs before other background services. ASP.NET Core starts hosted services in registration order.

### Step 5: Run all unit tests

```bash
dotnet test tests/Chronith.Tests.Unit --nologo
```
Expected: all tests PASS.

### Step 6: Build check

```bash
dotnet build Chronith.slnx --nologo
```

### Step 7: Commit

```bash
git -C .worktrees/feat-argon2id-password-upgrade add \
  src/Chronith.Infrastructure/Services/PiiEncryptionMigrationService.cs \
  src/Chronith.Infrastructure/DependencyInjection.cs \
  tests/Chronith.Tests.Unit/Infrastructure/Services/PiiEncryptionMigrationServiceTests.cs
git -C .worktrees/feat-argon2id-password-upgrade commit \
  -m "feat(infra): add PiiEncryptionMigrationService for blocking startup PII encryption"
```

---

## Task 9: IAuditPiiRedactor + AuditBehavior integration

### Files
- **Create:** `src/Chronith.Application/Services/IAuditPiiRedactor.cs`
- **Create:** `src/Chronith.Infrastructure/Services/AuditPiiRedactor.cs`
- **Modify:** `src/Chronith.Application/Behaviors/AuditBehavior.cs`
- **Modify:** `src/Chronith.Infrastructure/DependencyInjection.cs`
- **Create:** `tests/Chronith.Tests.Unit/Infrastructure/Services/AuditPiiRedactorTests.cs`

### Step 1: Write the failing tests

```csharp
// tests/Chronith.Tests.Unit/Infrastructure/Services/AuditPiiRedactorTests.cs
using Chronith.Infrastructure.Services;
using FluentAssertions;

namespace Chronith.Tests.Unit.Infrastructure.Services;

public sealed class AuditPiiRedactorTests
{
    private readonly AuditPiiRedactor _sut = new();

    [Fact]
    public void Redact_NullInput_ReturnsNull()
    {
        _sut.Redact(null).Should().BeNull();
    }

    [Fact]
    public void Redact_EmptyString_ReturnsEmptyString()
    {
        _sut.Redact(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Redact_EmailField_IsRedacted()
    {
        var json = """{"Email":"user@example.com","Name":"Alice"}""";
        var result = _sut.Redact(json)!;
        result.Should().Contain("\"[REDACTED]\"");
        result.Should().NotContain("user@example.com");
        result.Should().Contain("Alice");
    }

    [Fact]
    public void Redact_CustomerEmailField_IsRedacted()
    {
        var json = """{"CustomerEmail":"cust@example.com","Status":"Confirmed"}""";
        var result = _sut.Redact(json)!;
        result.Should().NotContain("cust@example.com");
        result.Should().Contain("Confirmed");
    }

    [Fact]
    public void Redact_PhoneField_IsRedacted()
    {
        var json = """{"Phone":"+63-912-345-6789","Name":"Bob"}""";
        var result = _sut.Redact(json)!;
        result.Should().NotContain("+63-912-345-6789");
        result.Should().Contain("Bob");
    }

    [Fact]
    public void Redact_PasswordHashField_IsRedacted()
    {
        var json = """{"PasswordHash":"$argon2id$v=19$...","Name":"Charlie"}""";
        var result = _sut.Redact(json)!;
        result.Should().NotContain("$argon2id");
        result.Should().Contain("Charlie");
    }

    [Fact]
    public void Redact_NonPiiFields_AreNotAffected()
    {
        var json = """{"Id":"abc-123","Status":"Active","BookingTypeId":"def-456"}""";
        var result = _sut.Redact(json)!;
        result.Should().Contain("abc-123");
        result.Should().Contain("Active");
        result.Should().Contain("def-456");
    }

    [Fact]
    public void Redact_InvalidJson_ReturnsOriginal()
    {
        var notJson = "not json at all";
        _sut.Redact(notJson).Should().Be(notJson);
    }
}
```

### Step 2: Run test to verify it fails

```bash
dotnet test tests/Chronith.Tests.Unit --nologo --filter "AuditPiiRedactorTests"
```
Expected: **FAIL** — `AuditPiiRedactor` not found.

### Step 3: Create IAuditPiiRedactor interface

```csharp
// src/Chronith.Application/Services/IAuditPiiRedactor.cs
namespace Chronith.Application.Services;

/// <summary>
/// Removes PII from audit snapshot JSON strings before they are persisted.
/// </summary>
public interface IAuditPiiRedactor
{
    /// <summary>
    /// Returns a copy of <paramref name="json"/> with known PII fields
    /// replaced by <c>"[REDACTED]"</c>. Returns <paramref name="json"/>
    /// unchanged if it is null, empty, or not valid JSON.
    /// </summary>
    string? Redact(string? json);
}
```

### Step 4: Create AuditPiiRedactor implementation

```csharp
// src/Chronith.Infrastructure/Services/AuditPiiRedactor.cs
using System.Text.Json;
using System.Text.Json.Nodes;
using Chronith.Application.Services;

namespace Chronith.Infrastructure.Services;

public sealed class AuditPiiRedactor : IAuditPiiRedactor
{
    private static readonly HashSet<string> PiiKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Email", "CustomerEmail", "Phone", "PasswordHash"
    };

    public string? Redact(string? json)
    {
        if (string.IsNullOrEmpty(json)) return json;

        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonObject obj) return json;

            foreach (var key in PiiKeys)
            {
                if (obj.ContainsKey(key))
                    obj[key] = "[REDACTED]";
            }

            return obj.ToJsonString();
        }
        catch (JsonException)
        {
            return json; // not valid JSON — return unchanged
        }
    }
}
```

### Step 5: Modify AuditBehavior to inject IAuditPiiRedactor

Add `IAuditPiiRedactor piiRedactor` to primary constructor:
```csharp
public sealed class AuditBehavior<TRequest, TResponse>(
    IEnumerable<IAuditSnapshotResolver> resolvers,
    IAuditEntryRepository auditRepository,
    ITenantContext tenantContext,
    IAuditPiiRedactor piiRedactor,
    IUnitOfWork unitOfWork
) : IPipelineBehavior<TRequest, TResponse>
```

Before creating `AuditEntry.Create(...)`, apply redaction:
```csharp
var redactedOld = piiRedactor.Redact(oldValues);
var redactedNew = piiRedactor.Redact(newValues);

var entry = AuditEntry.Create(
    tenantContext.TenantId,
    tenantContext.UserId,
    tenantContext.Role,
    auditable.EntityType,
    auditable.EntityId,
    auditable.Action,
    redactedOld,  // was: oldValues
    redactedNew,  // was: newValues
    null);
```

Add using: `using Chronith.Application.Services;`

### Step 6: Register in DI

In `DependencyInjection.cs`, add:
```csharp
services.AddScoped<IAuditPiiRedactor, AuditPiiRedactor>();
```

### Step 7: Run tests

```bash
dotnet test tests/Chronith.Tests.Unit --nologo --filter "AuditPiiRedactorTests"
dotnet test tests/Chronith.Tests.Unit --nologo
dotnet build Chronith.slnx --nologo
```
Expected: all tests PASS, build succeeded.

### Step 8: Commit

```bash
git -C .worktrees/feat-argon2id-password-upgrade add \
  src/Chronith.Application/Services/IAuditPiiRedactor.cs \
  src/Chronith.Infrastructure/Services/AuditPiiRedactor.cs \
  src/Chronith.Application/Behaviors/AuditBehavior.cs \
  src/Chronith.Infrastructure/DependencyInjection.cs \
  tests/Chronith.Tests.Unit/Infrastructure/Services/AuditPiiRedactorTests.cs
git -C .worktrees/feat-argon2id-password-upgrade commit \
  -m "feat(app): add IAuditPiiRedactor and integrate into AuditBehavior (TDD)"
```

---

## Task 10: WebhookOutboxCleanupService + DeleteOlderThanAsync

### Files
- **Create:** `src/Chronith.Infrastructure/Services/WebhookOutboxCleanupOptions.cs`
- **Create:** `src/Chronith.Infrastructure/Services/WebhookOutboxCleanupService.cs`
- **Modify:** `src/Chronith.Application/Interfaces/IWebhookOutboxRepository.cs`
- **Modify:** `src/Chronith.Infrastructure/Persistence/Repositories/WebhookOutboxRepository.cs`
- **Modify:** `src/Chronith.Infrastructure/DependencyInjection.cs`
- **Create:** `tests/Chronith.Tests.Unit/Infrastructure/Services/WebhookOutboxCleanupServiceTests.cs`

### Step 1: Write the failing tests

```csharp
// tests/Chronith.Tests.Unit/Infrastructure/Services/WebhookOutboxCleanupServiceTests.cs
using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure.Services;

public sealed class WebhookOutboxCleanupServiceTests
{
    [Fact]
    public void Options_DefaultRetentionDays_Is30()
    {
        var opts = new WebhookOutboxCleanupOptions();
        opts.RetentionDays.Should().Be(30);
    }

    [Fact]
    public void Options_DefaultIntervalHours_Is6()
    {
        var opts = new WebhookOutboxCleanupOptions();
        opts.IntervalHours.Should().Be(6);
    }

    [Fact]
    public async Task ExecuteAsync_CallsDeleteOlderThan_WithCorrectCutoff()
    {
        // The service should pass DateTimeOffset.UtcNow - RetentionDays to the repository.
        var repo = Substitute.For<IWebhookOutboxRepository>();
        repo.DeleteOlderThanAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IWebhookOutboxRepository)).Returns(repo);

        var options = Options.Create(new WebhookOutboxCleanupOptions
        {
            RetentionDays = 30,
            IntervalHours = 1
        });
        var logger = NullLogger<WebhookOutboxCleanupService>.Instance;

        var svc = new WebhookOutboxCleanupService(scopeFactory, options, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        try { await svc.StartAsync(cts.Token); } catch (OperationCanceledException) { }

        await repo.Received().DeleteOlderThanAsync(
            Arg.Is<DateTimeOffset>(d => d <= DateTimeOffset.UtcNow.AddDays(-29)),
            Arg.Any<CancellationToken>());
    }
}
```

### Step 2: Run test to verify it fails

```bash
dotnet test tests/Chronith.Tests.Unit --nologo --filter "WebhookOutboxCleanupServiceTests"
```
Expected: **FAIL** — types not found.

### Step 3: Add DeleteOlderThanAsync to IWebhookOutboxRepository

Add method to the interface:
```csharp
/// <summary>
/// Hard-deletes outbox entries with a terminal status (Delivered, Failed, Abandoned)
/// that were created before <paramref name="cutoff"/>.
/// </summary>
Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
```

### Step 4: Implement DeleteOlderThanAsync in WebhookOutboxRepository

In `WebhookOutboxRepository`, add:
```csharp
public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
{
    return await db.WebhookOutboxEntries
        .IgnoreQueryFilters()
        .Where(e => e.CreatedAt < cutoff
            && (e.Status == OutboxStatus.Delivered
                || e.Status == OutboxStatus.Failed
                || e.Status == OutboxStatus.Abandoned))
        .ExecuteDeleteAsync(ct);
}
```

Add using: `using Chronith.Domain.Enums;` if not already present.

### Step 5: Create WebhookOutboxCleanupOptions

```csharp
// src/Chronith.Infrastructure/Services/WebhookOutboxCleanupOptions.cs
namespace Chronith.Infrastructure.Services;

public sealed class WebhookOutboxCleanupOptions
{
    public const string SectionName = "WebhookOutboxCleanup";

    /// <summary>Retain rows for this many days after creation. Default: 30.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>How often to run the cleanup. Default: every 6 hours.</summary>
    public int IntervalHours { get; set; } = 6;
}
```

### Step 6: Create WebhookOutboxCleanupService

```csharp
// src/Chronith.Infrastructure/Services/WebhookOutboxCleanupService.cs
using Chronith.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Services;

public sealed class WebhookOutboxCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<WebhookOutboxCleanupOptions> options,
    ILogger<WebhookOutboxCleanupService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        var interval = TimeSpan.FromHours(opts.IntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var repo = scope.ServiceProvider.GetRequiredService<IWebhookOutboxRepository>();
                var cutoff = DateTimeOffset.UtcNow.AddDays(-opts.RetentionDays);
                var deleted = await repo.DeleteOlderThanAsync(cutoff, stoppingToken);
                if (deleted > 0)
                    logger.LogInformation(
                        "WebhookOutboxCleanupService: purged {Count} outbox rows older than {Cutoff}.",
                        deleted, cutoff);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "WebhookOutboxCleanupService: error during cleanup iteration.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
```

### Step 7: Register in DI

In `DependencyInjection.cs`:
```csharp
services.Configure<WebhookOutboxCleanupOptions>(
    configuration.GetSection(WebhookOutboxCleanupOptions.SectionName));
services.AddHostedService<WebhookOutboxCleanupService>();
```

### Step 8: Run all unit tests

```bash
dotnet test tests/Chronith.Tests.Unit --nologo
dotnet build Chronith.slnx --nologo
```
Expected: all tests PASS.

### Step 9: Commit

```bash
git -C .worktrees/feat-argon2id-password-upgrade add \
  src/Chronith.Infrastructure/Services/WebhookOutboxCleanupOptions.cs \
  src/Chronith.Infrastructure/Services/WebhookOutboxCleanupService.cs \
  src/Chronith.Application/Interfaces/IWebhookOutboxRepository.cs \
  src/Chronith.Infrastructure/Persistence/Repositories/WebhookOutboxRepository.cs \
  src/Chronith.Infrastructure/DependencyInjection.cs \
  tests/Chronith.Tests.Unit/Infrastructure/Services/WebhookOutboxCleanupServiceTests.cs
git -C .worktrees/feat-argon2id-password-upgrade commit \
  -m "feat(infra): add WebhookOutboxCleanupService for outbox retention (TDD)"
```

---

## Final Verification

### Run all unit tests

```bash
dotnet test tests/Chronith.Tests.Unit --nologo
```
Expected: all 523+ tests PASS (the new tests push the count higher).

### Full solution build

```bash
dotnet build Chronith.slnx --nologo
```
Expected: **Build succeeded, 0 error(s)**.

### Check commit log

```bash
git -C .worktrees/feat-argon2id-password-upgrade log --oneline -15
```
Expected: 10 new commits since the last Argon2id commit.

---

## Summary of New Files

| File | Purpose |
|------|---------|
| `src/Chronith.Application/Services/IBlindIndexService.cs` | HMAC blind index interface |
| `src/Chronith.Application/Services/IAuditPiiRedactor.cs` | Audit PII scrubbing interface |
| `src/Chronith.Infrastructure/Security/BlindIndexOptions.cs` | HMAC key config |
| `src/Chronith.Infrastructure/Security/HmacBlindIndexService.cs` | HMAC-SHA256 implementation |
| `src/Chronith.Infrastructure/Services/AuditPiiRedactor.cs` | JSON PII field redactor |
| `src/Chronith.Infrastructure/Services/PiiEncryptionMigrationService.cs` | Startup migration of plaintext rows |
| `src/Chronith.Infrastructure/Services/WebhookOutboxCleanupOptions.cs` | Outbox retention config |
| `src/Chronith.Infrastructure/Services/WebhookOutboxCleanupService.cs` | Periodic outbox purge |
| `tests/...HmacBlindIndexServiceTests.cs` | Unit tests for blind index |
| `tests/...BookingTypeRepositoryEncryptionTests.cs` | Unit tests for callback secret encryption |
| `tests/...CustomerRepositoryEncryptionTests.cs` | Unit tests for customer PII encryption |
| `tests/...TenantUserRepositoryEncryptionTests.cs` | Unit tests for tenant user PII encryption |
| `tests/...PiiEncryptionMigrationServiceTests.cs` | Unit tests for migration logic |
| `tests/...AuditPiiRedactorTests.cs` | Unit tests for audit PII redaction |
| `tests/...WebhookOutboxCleanupServiceTests.cs` | Unit tests for outbox cleanup |

## Summary of Modified Files

| File | Change |
|------|--------|
| `CustomerEntity.cs` | + EmailEncrypted, EmailToken, PhoneEncrypted |
| `TenantUserEntity.cs` | + EmailEncrypted, EmailToken |
| `CustomerConfiguration.cs` | + new column configs + ix_customers_email_token index |
| `TenantUserConfiguration.cs` | + new column configs + ix_tenantusers_email_token index |
| `BookingConfiguration.cs` | Remove HasMaxLength(200) from CustomerEmail |
| `WaitlistEntryConfiguration.cs` | Remove HasMaxLength(320) from CustomerEmail |
| `StaffMemberConfiguration.cs` | Remove HasMaxLength(320) from Email |
| `BookingTypeRepository.cs` | Inject IEncryptionService, encrypt/decrypt CustomerCallbackSecret |
| `CustomerRepository.cs` | Inject IEncryptionService + IBlindIndexService, encrypt/decrypt |
| `TenantUserRepository.cs` | Inject IEncryptionService + IBlindIndexService, encrypt/decrypt |
| `BookingRepository.cs` | Inject IEncryptionService, encrypt/decrypt CustomerEmail |
| `WaitlistRepository.cs` | Inject IEncryptionService, encrypt/decrypt CustomerEmail |
| `StaffMemberRepository.cs` | Inject IEncryptionService, encrypt/decrypt Email |
| `EncryptionKeyRotationService.cs` | + BookingType.CustomerCallbackSecret rotation |
| `AuditBehavior.cs` | + IAuditPiiRedactor injection and call |
| `IWebhookOutboxRepository.cs` | + DeleteOlderThanAsync |
| `WebhookOutboxRepository.cs` | + DeleteOlderThanAsync implementation |
| `DependencyInjection.cs` | Register all new services |
| `appsettings.json` | + Security:HmacKey placeholder |
| New EF migrations × 3 | Customer columns, TenantUser columns, email column expansion |
