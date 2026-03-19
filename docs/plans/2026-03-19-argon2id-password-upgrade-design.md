# Argon2id Password Upgrade — Design Doc

**Date:** 2026-03-19
**Branch:** `feat/argon2id-password-upgrade`
**Status:** Approved for implementation

---

## Goal

Replace BCrypt with Argon2id for all password hashing in the Chronith API, and fix a clean architecture violation where `Chronith.Application` directly depends on `BCrypt.Net-Next`, an infrastructure-level package.

---

## Background

### Why replace BCrypt?

BCrypt was the industry standard for password hashing for over two decades. It remains secure, but has limitations:

- **Time-cost only:** BCrypt's work factor (`$2a$12$`) controls CPU iterations but has no memory cost parameter. This means GPU clusters and ASICs can run thousands of parallel brute-force attempts cheaply.
- **54-byte input limit:** BCrypt silently truncates passwords longer than 72 bytes, which creates subtle security bugs for long passphrases.
- **Not the current OWASP recommendation:** OWASP's [Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html) now recommends Argon2id as the first choice.

### Why Argon2id?

Argon2id won the [Password Hashing Competition](https://password-hashing.net/) in 2015 and is specified in [RFC 9106](https://www.rfc-editor.org/rfc/rfc9106).

- **Memory-hard:** Forces attackers to use large amounts of RAM per attempt, making GPU/ASIC brute-force attacks dramatically more expensive.
- **Configurable:** Separate parameters for memory cost (`m`), time cost (`t`), and parallelism (`p`).
- **Hybrid design:** Argon2id combines Argon2i (side-channel resistant data access) with Argon2d (GPU resistance), making it appropriate for server-side password hashing where side-channel attacks are less of a concern but GPU attacks are.

**OWASP recommended minimum parameters (as of 2024):**

| Parameter | Value | Meaning |
|-----------|-------|---------|
| `m` | 65536 (64 MB) | Memory cost in KiB |
| `t` | 3 | Number of iterations |
| `p` | 4 | Degree of parallelism |

These parameters are used in this implementation.

### Why fix the architecture violation?

`Chronith.Application.csproj` currently has a direct `PackageReference` to `BCrypt.Net-Next`. This violates the project's layered architecture:

```
Domain  ←  Application  ←  Infrastructure  ←  API
```

The Application layer should define *interfaces* for infrastructure concerns, not implement them. Password hashing is an infrastructure concern (it requires a specific algorithm and library). The Application layer should declare what it needs (`IPasswordHasher`) and the Infrastructure layer should provide the implementation (`Argon2idPasswordHasher`).

**Benefits of the interface:**
- Application handlers become testable with mock password hashers (no bcrypt overhead in unit tests)
- The hashing algorithm can be swapped without touching Application code
- The dependency graph is clean — Application has zero infrastructure package dependencies

---

## What Changes

### 1. New interface: `IPasswordHasher`

**File:** `src/Chronith.Application/Services/IPasswordHasher.cs`

```csharp
namespace Chronith.Application.Services;

public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password. The returned string is self-contained
    /// and includes the algorithm identifier, parameters, salt, and hash.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// Verifies a plaintext password against a previously produced hash.
    /// Returns true if the password matches, false otherwise.
    /// </summary>
    bool Verify(string password, string hash);
}
```

This interface lives in `Chronith.Application/Services/` alongside `IEncryptionService`, `IEmailNotificationService`, etc.

### 2. New implementation: `Argon2idPasswordHasher`

**File:** `src/Chronith.Infrastructure/Security/Argon2idPasswordHasher.cs`
**NuGet package:** `Isopoh.Cryptography.Argon2`

```csharp
using Chronith.Application.Services;
using Isopoh.Cryptography.Argon2;

namespace Chronith.Infrastructure.Security;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    // OWASP recommended minimum parameters for Argon2id (2024)
    private const int MemoryCostKib = 65536; // 64 MB
    private const int Iterations = 3;
    private const int Parallelism = 4;

    public string Hash(string password)
    {
        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing, // Argon2id
            Version = Argon2Version.Nineteen,
            MemoryCost = MemoryCostKib,
            TimeCost = Iterations,
            Lanes = Parallelism,
            Threads = Parallelism,
            Password = System.Text.Encoding.UTF8.GetBytes(password),
            HashLength = 32,
        };

        using var argon2 = new Argon2(config);
        using var hash = argon2.Hash();
        return config.EncodeString(hash.Buffer);
    }

    public bool Verify(string password, string hash)
    {
        return Argon2.Verify(hash, password);
    }
}
```

The encoded string format is the standard Argon2 PHC string format:
`$argon2id$v=19$m=65536,t=3,p=4$<salt>$<hash>`

This is self-describing — the algorithm, version, and parameters are embedded in the hash string, so future parameter changes are backward-compatible for verification.

### 3. Update 6 command handlers

All six handlers currently call `BCrypt.Net.BCrypt.HashPassword(...)` or `BCrypt.Net.BCrypt.Verify(...)` directly. Each will be updated to inject `IPasswordHasher` via primary constructor and delegate to it.

| Handler | File | Change |
|---------|------|--------|
| `SignupCommandHandler` | `src/Chronith.Application/Features/Auth/SignupCommand.cs` | Inject `IPasswordHasher`, replace BCrypt calls |
| `RegisterTenantCommandHandler` | `src/Chronith.Application/Features/Tenants/RegisterTenantCommand.cs` | Inject `IPasswordHasher`, replace BCrypt calls |
| `CustomerRegisterCommandHandler` | `src/Chronith.Application/Features/Customers/CustomerRegisterCommand.cs` | Inject `IPasswordHasher`, replace BCrypt calls |
| `UpdateMeCommandHandler` | `src/Chronith.Application/Features/Users/UpdateMeCommand.cs` | Inject `IPasswordHasher`, replace BCrypt calls |
| `LoginCommandHandler` | `src/Chronith.Application/Features/Auth/LoginCommand.cs` | Inject `IPasswordHasher`, replace BCrypt calls |
| `CustomerLoginCommandHandler` | `src/Chronith.Application/Features/Customers/CustomerLoginCommand.cs` | Inject `IPasswordHasher`, replace BCrypt calls |

**Pattern (before):**
```csharp
var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password);
// ...
if (!BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash))
    throw new UnauthorizedException("Invalid credentials.");
```

**Pattern (after):**
```csharp
// Constructor:
public sealed class SignupCommandHandler(
    IUserRepository userRepo,
    IPasswordHasher passwordHasher,  // ← new
    IUnitOfWork unitOfWork
) : IRequestHandler<SignupCommand, AuthTokenDto> { ... }

// Usage:
var passwordHash = passwordHasher.Hash(command.Password);
// ...
if (!passwordHasher.Verify(command.Password, user.PasswordHash))
    throw new UnauthorizedException("Invalid credentials.");
```

### 4. DI registration

**File:** `src/Chronith.Infrastructure/DependencyInjection.cs` (or equivalent)

```csharp
services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
```

Registered as singleton because `Argon2idPasswordHasher` is stateless — the configuration parameters are constants and there is no shared mutable state.

### 5. Remove BCrypt from Application project

**File:** `src/Chronith.Application/Chronith.Application.csproj`

Remove:
```xml
<PackageReference Include="BCrypt.Net-Next" Version="4.x.x" />
```

`BCrypt.Net-Next` remains in `Chronith.Infrastructure.csproj` only until the migration is complete. Once all handlers are updated, it is removed from the Infrastructure project too (it is fully replaced by `Isopoh.Cryptography.Argon2`).

### 6. EF migration: `CustomerEntity.PasswordHash` varchar → text

`TenantUserEntity.PasswordHash` is already `text` in the database. `CustomerEntity.PasswordHash` is currently `varchar(200)`.

Argon2id PHC strings are longer than BCrypt hashes:
- BCrypt: `$2a$12$<53 chars>` = 60 characters total
- Argon2id: `$argon2id$v=19$m=65536,t=3,p=4$<22 chars salt>$<43 chars hash>` = ~96 characters

`varchar(200)` is sufficient for Argon2id with these parameters, but for consistency with `TenantUserEntity` and to avoid future truncation issues if parameters are increased, the column is widened to `text`.

**Migration name:** `ChangeCustomerPasswordHashToText`

```bash
dotnet ef migrations add ChangeCustomerPasswordHashToText \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API \
  --output-dir Migrations/PostgreSQL
```

**No data migration needed.** There are no real users in the system (no production data). Existing test/seed data will be re-created.

---

## What Does NOT Change

- The `IEncryptionService` / `AesGcmEncryptionService` for settings blobs — unrelated to password hashing
- The JWT token generation/validation logic
- The API key and refresh token SHA-256 hashing (these are single-use tokens, not user passwords — a different threat model)
- Any database schema other than `CustomerEntity.PasswordHash`

---

## Testing Strategy

### Unit tests

Each of the 6 updated command handlers gets updated unit tests:
- Replace `BCrypt` mock/fake with a mock `IPasswordHasher` (NSubstitute)
- Existing test coverage is maintained

New unit tests for `Argon2idPasswordHasher`:
- `Hash_ReturnsSelfDescribingArgon2idString` — verify the encoded string starts with `$argon2id$`
- `Verify_ReturnsTrueForCorrectPassword`
- `Verify_ReturnsFalseForIncorrectPassword`
- `Hash_ProducesDifferentHashesForSamePassword` — verify random salt per call

### Integration tests

No new integration tests required. Existing integration tests that exercise auth endpoints will continue to pass as the hash/verify behavior is functionally identical.

### Performance note

Argon2id with `m=65536, t=3, p=4` takes approximately 200–400ms per hash on typical server hardware (the memory allocation is the bottleneck). BCrypt WF12 takes approximately 200–300ms. The performance impact is comparable. This is intentional — the cost deters brute-force attacks.

For unit tests, consider using a faster `IPasswordHasher` mock rather than the real implementation to avoid slow test suites.

---

## Rollout

1. Implement `IPasswordHasher` interface + `Argon2idPasswordHasher` (TDD)
2. Update 6 handlers to inject `IPasswordHasher` (one at a time, with tests)
3. Register in DI, remove BCrypt from Application project
4. Add EF migration for `CustomerEntity.PasswordHash`
5. Run full test suite
6. Commit and open PR

No feature flag needed — this is a pure refactor with no behavioral change from the user's perspective.

---

## References

- [RFC 9106 — Argon2 Memory-Hard Function](https://www.rfc-editor.org/rfc/rfc9106)
- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
- [Isopoh.Cryptography.Argon2 NuGet](https://www.nuget.org/packages/Isopoh.Cryptography.Argon2)
- [Password Hashing Competition](https://password-hashing.net/)
