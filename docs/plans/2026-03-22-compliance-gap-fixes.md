# Compliance Gap Fixes — Implementation Plan

**Date:** 2026-03-22  
**Branch:** `feat/compliance-gap-fixes`  
**Base:** `main` @ `988753f`

---

## Findings to Address

| ID       | Severity | Description                                                                                     |
| -------- | -------- | ----------------------------------------------------------------------------------------------- |
| INF-001  | Medium   | `WebhookRepository.GetByIdCrossTenantAsync` missing `!w.IsDeleted` guard                        |
| SEC-001  | Critical | `GetMeEndpoint` and `PatchMeEndpoint` have no explicit `Roles()` or `AuthSchemes()`             |
| TEST-001 | Medium   | Missing auth test files for `ApiKeys/`, `Plans/`, `Signup/` endpoint groups                     |
| ARCH-002 | Medium   | 9 command subdirs in `Auth/` and `CustomerAuth/` use split files instead of single-file pattern |
| ARCH-001 | High     | `ApiKeyScope` lives in `Chronith.Domain.Models` but should be in `Chronith.Application.Models`  |

---

## Tasks

### Task 1 — INF-001: WebhookRepository soft-delete guard

**Files:**

- `tests/Chronith.Tests.Integration/Repositories/WebhookRepositoryTests.cs` — add failing test
- `src/Chronith.Infrastructure/Persistence/Repositories/WebhookRepository.cs` — add `&& !w.IsDeleted`

**Steps:**

1. Add test `GetByIdCrossTenantAsync_SoftDeletedWebhook_ReturnsNull` — expect `null` for a soft-deleted webhook
2. Run test → RED
3. In `GetByIdCrossTenantAsync`, change predicate to include `&& !w.IsDeleted`
4. Run test → GREEN
5. Build, commit: `fix(infra): exclude soft-deleted webhooks from cross-tenant lookup`

---

### Task 2 — SEC-001: Explicit auth on MeEndpoints

**Files:**

- `tests/Chronith.Tests.Functional/Auth/MeTests.cs` — add 2 failing tests
- `src/Chronith.API/Endpoints/Auth/MeEndpoint.cs` — add `Roles()` + `AuthSchemes()`

**Steps:**

1. Add tests `GetMe_WithApiKey_Returns401` and `PatchMe_WithApiKey_Returns401`
2. Run tests → RED (currently API key may be accepted or default behavior applies)
3. In `GetMeEndpoint.Configure()`, add `Roles("TenantAdmin", "TenantStaff")` and `AuthSchemes("Bearer")`
4. In `PatchMeEndpoint.Configure()`, add `Roles("TenantAdmin", "TenantStaff")` and `AuthSchemes("Bearer")`
5. Run tests → GREEN
6. Build, commit: `fix(api): restrict /me endpoints to Bearer auth only`

---

### Task 3 — TEST-001: ApiKeyAuthTests

**Files:**

- `tests/Chronith.Tests.Functional/ApiKeys/ApiKeyAuthTests.cs` — create

**Steps:**

1. Create `ApiKeyAuthTests.cs` covering role-based access for `GET /api-keys`, `POST /api-keys`, `DELETE /api-keys/{id}`
2. Tests: anonymous → 401, Customer → 403, TenantStaff → 403, TenantAdmin → 200/201/204
3. For Create/Delete: API key auth → 401 (Bearer-only)
4. For List: API key with scope → 200
5. Run tests → GREEN (using existing auth wiring)
6. Build, commit: `test(functional): add ApiKeyAuthTests for role-based access`

---

### Task 4 — TEST-001: PlansAuthTests + SignupAuthTests

**Files:**

- `tests/Chronith.Tests.Functional/Plans/PlansAuthTests.cs` — create
- `tests/Chronith.Tests.Functional/Signup/SignupAuthTests.cs` — create

**Steps:**

1. Create `PlansAuthTests.cs`: `GetPlans_Anonymous_Returns200` (public endpoint)
2. Create `SignupAuthTests.cs`: `Signup_Anonymous_Returns201`, `VerifyEmail_Anonymous_Returns200`
3. Run tests → GREEN
4. Build, commit: `test(functional): add PlansAuthTests and SignupAuthTests for public endpoints`

---

### Task 5 — ARCH-002: Consolidate Auth split command files

**Directories to merge (each has Command.cs + Handler.cs [+ Validator.cs]):**

- `Auth/Login/`
- `Auth/Refresh/`
- `Auth/Register/`
- `Auth/UpdateMe/`

**Steps (for each directory):**

1. Read all split files
2. Merge into single `*Command.cs` file (record + validator + handler in order)
3. Delete the now-empty `Handler.cs` and `Validator.cs` files
4. Build → no errors
5. After all 4: run unit tests → GREEN
6. Commit: `refactor(application): consolidate Auth command files into single-file pattern`

---

### Task 6 — ARCH-002: Consolidate CustomerAuth split command files

**Directories to merge:**

- `CustomerAuth/Login/`
- `CustomerAuth/OidcLogin/`
- `CustomerAuth/Refresh/`
- `CustomerAuth/Register/`
- `CustomerAuth/UpdateProfile/`

**Steps (same as Task 5):**

1. Read all split files per directory
2. Merge into single `*Command.cs`
3. Delete split files
4. Build → no errors
5. Run unit tests → GREEN
6. Commit: `refactor(application): consolidate CustomerAuth command files into single-file pattern`

---

### Task 7 — ARCH-001: Move ApiKeyScope to Application layer

**Files:**

- `src/Chronith.Domain/Models/ApiKeyScope.cs` — DELETE
- `src/Chronith.Application/Models/ApiKeyScope.cs` — CREATE with namespace `Chronith.Application.Models`
- `src/Chronith.API/Program.cs` — update fully-qualified reference
- ~78 endpoint files with `using Chronith.Domain.Models;` → `using Chronith.Application.Models;`
- `tests/Chronith.Tests.Unit/Domain/ApiKeyScopeTests.cs` — update namespace import
- Functional test files with `using Chronith.Domain.Models;` for ApiKeyScope

**Steps:**

1. Create `src/Chronith.Application/Models/ApiKeyScope.cs` with new namespace
2. Update `Program.cs` fully-qualified reference
3. Batch-replace `using Chronith.Domain.Models;` in endpoint files (where it imports ApiKeyScope)
4. Update unit test file namespace import
5. Batch-replace in functional test files
6. Delete `src/Chronith.Domain/Models/ApiKeyScope.cs`
7. Build → no errors
8. Run all tests → GREEN
9. Commit: `refactor(domain): move ApiKeyScope to Application layer`

---

## Key Facts

- `TenantUserRole.Owner` and `TenantUserRole.Admin` both map to JWT role `"TenantAdmin"` — there is no separate "Owner" claim.
- `GetPlansEndpoint`, `SignupEndpoint`, `VerifyEmailEndpoint` all use `AllowAnonymous()` — TEST-001 for Plans/Signup are "trivial public access" tests.
- `CreateApiKeyEndpoint` and `RevokeApiKeyEndpoint` are **intentionally Bearer-only** — do not add ApiKey auth to these.
- Currency: PHP only, prices stored as `long` in centavos.
- `Security:EncryptionKey` placeholder in appsettings is not safe for production.

---

## Commit Convention

All commits use conventional commits with scope, e.g.:

- `fix(infra): ...`
- `fix(api): ...`
- `refactor(application): ...`
- `refactor(domain): ...`
- `test(functional): ...`
