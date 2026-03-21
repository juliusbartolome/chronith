# API Key Scope-Based RBAC — Design Doc

**Date:** 2026-03-21
**Branch:** `feat/api-key-scopes`
**Status:** Approved for implementation

---

## Goal

Replace the free-form `Role` string on `TenantApiKey` with a set of explicit, fine-grained scopes (e.g. `bookings:read`, `staff:write`). Each API key carries exactly the scopes it needs — nothing more. This makes API key permissions auditable, minimal by default, and aligned with OAuth2 conventions.

---

## Background

### Current state

API keys carry a single `Role : string` field validated only for non-emptiness and max-length. The `ApiKeyAuthenticationHandler` emits this string as a `ClaimTypes.Role` claim, which is then checked by FastEndpoints `AllowRoles(...)`. There is no whitelist of valid roles and no concept of minimal permission grants.

### Why change?

- A key created for a payment webhook (`POST /bookings/{id}/pay`) currently needs to be given `TenantPaymentService` — a role name that happens to work — but nothing prevents assigning it `TenantAdmin` and granting full access.
- There is no way to create a read-only API key for analytics export, a key scoped only to availability lookups, or any other least-privilege grant.
- The free-form string is opaque in logs and audit records.

### Design decisions

| Decision           | Choice                               | Rationale                                                                                                                    |
| ------------------ | ------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------- |
| Storage            | `text[]` PostgreSQL array column     | Human-readable, OAuth2-idiomatic, first-class Npgsql support, no join on hot path                                            |
| Auth model         | Scope-only (scope IS the permission) | API keys are already tenant-scoped by FK; a scope like `bookings:read` unambiguously grants that action for the key's tenant |
| Creator constraint | Scopes bounded by creator's role     | A `TenantAdmin` can grant any scope; the check is written generically to support narrower roles in future                    |
| Who can create     | `TenantAdmin` only (unchanged)       | Keeps surface area minimal; TenantStaff key creation can be added later                                                      |

---

## Scope Taxonomy

Fifteen well-known scopes covering the full API surface:

| Scope                 | Grants                                                                         |
| --------------------- | ------------------------------------------------------------------------------ |
| `bookings:read`       | `GET /bookings`, `GET /bookings/{id}`                                          |
| `bookings:write`      | `POST /bookings`, `PUT /bookings/{id}/reschedule`                              |
| `bookings:delete`     | `DELETE /bookings/{id}`                                                        |
| `bookings:confirm`    | `POST /bookings/{id}/confirm`, `POST /bookings/{id}/assign-staff`              |
| `bookings:cancel`     | `POST /bookings/{id}/cancel`                                                   |
| `bookings:pay`        | `POST /bookings/{id}/pay`                                                      |
| `availability:read`   | `GET /booking-types/{slug}/availability`, `GET /availability/slots`            |
| `staff:read`          | `GET /staff`, `GET /staff/{id}`                                                |
| `staff:write`         | `POST /staff`, `PUT /staff/{id}`, `POST /staff/{id}/deactivate`, time blocks   |
| `booking-types:read`  | `GET /booking-types`, `GET /booking-types/{id}`                                |
| `booking-types:write` | `POST /booking-types`, `PUT /booking-types/{id}`, `DELETE /booking-types/{id}` |
| `analytics:read`      | `GET /analytics`, `GET /metrics`                                               |
| `webhooks:write`      | `POST /webhooks`, `PUT /webhooks/{id}`, `DELETE /webhooks/{id}`                |
| `tenant:read`         | `GET /tenant`                                                                  |
| `tenant:write`        | `PUT /tenant`, subscriptions, payment config, notification settings            |

---

## What Changes

### 1. Domain — `TenantApiKey` and `ApiKeyScope`

**New file:** `src/Chronith.Domain/Models/ApiKeyScope.cs`

```csharp
namespace Chronith.Domain.Models;

public static class ApiKeyScope
{
    public const string BookingsRead    = "bookings:read";
    public const string BookingsWrite   = "bookings:write";
    public const string BookingsDelete  = "bookings:delete";
    public const string BookingsConfirm = "bookings:confirm";
    public const string BookingsCancel  = "bookings:cancel";
    public const string BookingsPay     = "bookings:pay";
    public const string AvailabilityRead = "availability:read";
    public const string StaffRead       = "staff:read";
    public const string StaffWrite      = "staff:write";
    public const string BookingTypesRead  = "booking-types:read";
    public const string BookingTypesWrite = "booking-types:write";
    public const string AnalyticsRead   = "analytics:read";
    public const string WebhooksWrite   = "webhooks:write";
    public const string TenantRead      = "tenant:read";
    public const string TenantWrite     = "tenant:write";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        BookingsRead, BookingsWrite, BookingsDelete, BookingsConfirm, BookingsCancel, BookingsPay,
        AvailabilityRead, StaffRead, StaffWrite, BookingTypesRead, BookingTypesWrite,
        AnalyticsRead, WebhooksWrite, TenantRead, TenantWrite,
    };
}
```

**Modified file:** `src/Chronith.Domain/Models/TenantApiKey.cs`

- Remove `Role : string` property
- Add `Scopes : IReadOnlyList<string>` (private backing `List<string>`)
- `Create(...)` factory: remove `role` parameter, add `IEnumerable<string> scopes`; validate each scope is in `ApiKeyScope.All`, throw `DomainException` on invalid scope or empty set

### 2. Infrastructure — Entity and EF Configuration

**Modified:** `src/Chronith.Infrastructure/Persistence/Entities/TenantApiKeyEntity.cs`

- Remove `Role : string`
- Add `Scopes : List<string>`

**Modified:** `src/Chronith.Infrastructure/Persistence/Configurations/TenantApiKeyConfiguration.cs`

- Remove `role` column mapping
- Add `scopes` column: `.HasColumnType("text[]")`

**New migration:** `AddApiKeyScopesDropRole`

```bash
dotnet ef migrations add AddApiKeyScopesDropRole \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API \
  --output-dir Migrations/PostgreSQL
```

Migration SQL:

```sql
ALTER TABLE chronith.tenant_api_keys ADD COLUMN scopes text[] NOT NULL DEFAULT '{}';
ALTER TABLE chronith.tenant_api_keys DROP COLUMN role;
```

> **Note:** Existing keys in any deployed environment will have empty `scopes` after migration. These keys will fail all scope checks and must be re-created. This is intentional — a key with unknown legacy role should not silently inherit permissions.

**Modified:** `src/Chronith.Infrastructure/Persistence/Mappers/TenantApiKeyMapper.cs`

- `ToEntity()`: map `Scopes` list
- `ToDomain()`: map `Scopes` list (use `SetBackingField` for the private `_scopes` list)

### 3. Application — Command, DTO, and Handler

**Modified:** `src/Chronith.Application/Commands/ApiKeys/CreateApiKeyCommand.cs`

Record changes:

```csharp
// Before
public required string Role { get; init; }

// After
public required IEnumerable<string> Scopes { get; init; }
```

Validator changes:

```csharp
// Before
RuleFor(x => x.Role).NotEmpty().MaximumLength(50);

// After
RuleFor(x => x.Scopes)
    .NotEmpty().WithMessage("At least one scope is required.")
    .ForEach(scope => scope
        .Must(s => ApiKeyScope.All.Contains(s))
        .WithMessage(s => $"'{s}' is not a valid scope."));
```

Handler changes — inject `ITenantContext`, validate scopes against creator's allowed set:

```csharp
// RoleToAllowedScopes — defined in Application layer, all roles currently map to ApiKeyScope.All
// because only TenantAdmin can create keys. The map is written generically for future extension.
private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RoleAllowedScopes =
    new Dictionary<string, IReadOnlySet<string>>
    {
        [Roles.TenantAdmin] = ApiKeyScope.All,
    };
```

**Modified:** `src/Chronith.Application/Queries/ApiKeys/GetApiKeysQuery.cs` (or wherever `ApiKeyDto` is defined)

DTO changes:

```csharp
// Before
public sealed record ApiKeyDto(Guid Id, string Description, string Role, ...);

// After
public sealed record ApiKeyDto(Guid Id, string Description, IReadOnlyList<string> Scopes, ...);
```

### 4. Infrastructure — Auth Handler

**Modified:** `src/Chronith.Infrastructure/Auth/ApiKeyAuthenticationHandler.cs`

```csharp
// Before
claims.Add(new Claim(ClaimTypes.Role, key.Role));

// After
// Emit one scope claim per granted scope
foreach (var scope in key.Scopes)
    claims.Add(new Claim("scope", scope));

// Synthetic role so AllowRoles("ApiKey") can gate API-key-capable endpoints
claims.Add(new Claim(ClaimTypes.Role, "ApiKey"));
```

### 5. API — Endpoint Authorization

**New helper:** `src/Chronith.API/Extensions/EndpointScopeExtensions.cs`

```csharp
public static class EndpointScopeExtensions
{
    /// <summary>
    /// Adds a policy requirement: if the request authenticated via ApiKey scheme,
    /// the caller must have the specified scope claim. JWT callers bypass this check.
    /// </summary>
    public static RouteHandlerBuilder RequireApiKeyScope(
        this RouteHandlerBuilder builder, string scope) { ... }
}
```

Implementation uses `IAuthorizationPolicyBuilder` with a custom `ApiKeyScopeRequirement` and `ApiKeyScopeHandler` (checks `context.User.HasClaim("scope", scope)` only when `AuthenticationScheme == "ApiKey"`).

**Modified endpoints:** Each endpoint that should be callable via API key adds `.RequireApiKeyScope(ApiKeyScope.XxxYyy)`. JWT callers are unaffected. Endpoints that have no API key use case (e.g. `POST /auth/login`) are not changed.

---

## What Does NOT Change

- JWT authentication and role-based authorization logic
- Who can create API keys (TenantAdmin only)
- Revoke / list API key endpoints (logic unchanged, DTO gains `Scopes`)
- API key generation and SHA-256 hashing (`TenantApiKey.GenerateKey()` / `ComputeHash()`)
- Any other domain model or repository

---

## Testing Strategy

Strict TDD: failing test first, then implement.

### Unit tests (`Chronith.Tests.Unit`)

| Test class                         | Cases                                                                                                                                            |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| `ApiKeyScopeTests`                 | `All` set contains exactly 15 scopes; each constant is in `All`; no duplicates                                                                   |
| `TenantApiKeyCreateTests`          | Valid scopes → succeeds; empty scopes → throws; unknown scope string → throws                                                                    |
| `CreateApiKeyCommandHandlerTests`  | Valid request → creates key with scopes; unknown scope → validation error; empty scopes → validation error; scope exceeding creator role → error |
| `ApiKeyAuthenticationHandlerTests` | Scopes emitted as `scope` claims; synthetic `ApiKey` role claim present; empty scopes → no scope claims but still `ApiKey` role                  |

### Integration tests (`Chronith.Tests.Integration`)

| Test class                    | Cases                                                                                                     |
| ----------------------------- | --------------------------------------------------------------------------------------------------------- |
| `TenantApiKeyRepositoryTests` | Save key with multiple scopes, reload → scopes preserved; save key with empty scopes, reload → empty list |

### Functional tests (`Chronith.Tests.Functional`)

**New file:** `ApiKeyEndpointsTests.cs`

- Create key with `bookings:read` scope → 201, response includes scopes
- Use key on `GET /bookings` (requires `bookings:read`) → 200
- Use key on `DELETE /bookings/{id}` (requires `bookings:delete`) without that scope → 403
- Create key with unknown scope → 400 validation error
- Create key with empty scopes → 400 validation error

**Modified file:** `ApiKeyAuthTests.cs` (or equivalent auth test file)

- Unauthenticated create key → 401
- JWT without TenantAdmin role create key → 403
- Revoke key without TenantAdmin role → 403

---

## Rollout

1. Domain: `ApiKeyScope` constants + `TenantApiKey` model changes (TDD)
2. Infrastructure: entity, EF config, mapper, migration (TDD — integration tests)
3. Application: `CreateApiKeyCommand` + `ApiKeyDto` + handler (TDD — unit tests)
4. Infrastructure/Auth: `ApiKeyAuthenticationHandler` scope claims (TDD — unit tests)
5. API: `ApiKeyScopeRequirement` + `ApiKeyScopeHandler` + `RequireApiKeyScope` extension (TDD — functional tests)
6. API: Wire `RequireApiKeyScope` on each applicable endpoint
7. Run full test suite — all green
8. Commit, open PR targeting `main`

---

## References

- [OAuth 2.0 Scopes (RFC 6749 §3.3)](https://www.rfc-editor.org/rfc/rfc6749#section-3.3)
- [FastEndpoints Authorization](https://fast-endpoints.com/docs/security)
- [Npgsql Array Type Mapping](https://www.npgsql.org/efcore/mapping/array.html)
- `AGENTS.md` — architecture rules, test strategy, layer ownership
