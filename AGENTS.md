# AGENTS.md — Chronith

This file is the single source of truth for any AI agent working on this codebase.
Read it fully before making any changes.

---

## 1. Project Overview

**Chronith** is a multi-tenant booking engine API built in .NET 10 using clean architecture.

| Concern          | Technology                                                                |
| ---------------- | ------------------------------------------------------------------------- |
| Web framework    | FastEndpoints 8.x                                                         |
| CQRS / Mediator  | MediatR 14.x                                                              |
| Validation       | FluentValidation 12.x                                                     |
| ORM              | EF Core 10.x + Npgsql                                                     |
| Database         | PostgreSQL 17                                                             |
| Cache            | Redis 8 (StackExchange.Redis)                                             |
| Auth             | JWT (HMAC symmetric) + API Key (`X-Api-Key`)                              |
| Password hashing | Argon2id (customer accounts)                                              |
| Observability    | OpenTelemetry (traces + metrics, OTLP export)                             |
| Logging          | Serilog (console sink)                                                    |
| Notifications    | MailKit (SMTP), Twilio (SMS), FirebaseAdmin (push)                        |
| Payments         | Pluggable `IPaymentProvider` (Stub / PayMongo)                            |
| Testing          | xUnit, FluentAssertions, NSubstitute, Testcontainers, BenchmarkDotNet, k6 |
| CI               | GitHub Actions (6 jobs), CodeQL                                           |
| Container        | Docker multi-stage, Docker Compose                                        |

Solution file: `Chronith.slnx` (.NET XML-based solution format).
SDK pinned to `10.0.100` in `global.json`.

---

## 2. Architecture & Layer Rules

Four layers with strict dependency direction:

```
Domain  ←  Application  ←  Infrastructure  ←  API
```

Domain has zero project references. Each layer may only depend on layers to its left.

### What belongs where

| Layer                     | Contains                                                                                                                                                                                            |
| ------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Chronith.Domain`         | Models (entities, value objects), enums, domain exceptions. No framework dependencies.                                                                                                              |
| `Chronith.Application`    | Commands, queries, handlers, validators, DTOs, mappers (domain → DTO), repository interfaces, service interfaces, MediatR notifications, behaviors.                                                 |
| `Chronith.Infrastructure` | EF Core DbContext, entity POCOs, entity configurations, entity mappers (entity ↔ domain), repository implementations, background services, notification channels, payment providers, caching, auth. |
| `Chronith.API`            | FastEndpoints endpoint classes, middleware (exception handling), health checks, `Program.cs` DI wiring.                                                                                             |

### Rules

- **No AutoMapper.** All mapping is manual via `static` extension methods.
- Application layer defines interfaces; Infrastructure implements them.
- API layer never references Domain directly for data — always through Application DTOs.

---

## 3. Domain Model Patterns

### Construction

- `public static T Create(...)` factory method. Never expose a public constructor.
- `internal T()` parameterless constructor for EF Core hydration only.

```csharp
public static StaffMember Create(Guid tenantId, string name, ...) { ... }
internal StaffMember() { } // ORM only
```

### Properties

- `{ get; private set; }` — external code cannot set.
- String properties default via `= string.Empty`.
- Nullable reference types used where appropriate (`string?`).

### Collections

- Private `List<T>` backing field + public `IReadOnlyList<T>` property via `.AsReadOnly()`.

```csharp
private readonly List<StaffAvailabilityWindow> _availabilityWindows = [];
public IReadOnlyList<StaffAvailabilityWindow> AvailabilityWindows => _availabilityWindows.AsReadOnly();
```

### State changes

- Named domain methods (`Pay()`, `Confirm()`, `Cancel()`, `AssignStaff()`) — never raw property setters.
- State transition validation inside the method. Throw `InvalidStateTransitionException` on invalid transitions.

### Key domain rules

- **`BookingType`** is `abstract` with two concrete subtypes: `TimeSlotBookingType` and `CalendarBookingType`.
- **`DomainException`** is `abstract`. Use concrete subclasses: `NotFoundException`, `ConflictException`, `InvalidStateTransitionException`, `SlotConflictException`, `CustomFieldValidationException`, `UnauthorizedException`, etc.
- **Currency:** PHP only. Prices stored as `long` in centavos.
- **Free bookings** (price = 0) skip `PendingPayment` and go directly to `PendingVerification`.
- **`BookingStatus`** enum has exactly 4 values: `PendingPayment`, `PendingVerification`, `Confirmed`, `Cancelled`.

---

## 4. Application Layer Patterns

### Commands

Single file containing: `record` (implements `IRequest<T>`) + `Validator` (`AbstractValidator<T>`) + `Handler`.

- Command records use `required` init-only properties.
- Handlers use primary constructor injection.
- Commands that mutate state inject `IUnitOfWork` and call `SaveChangesAsync`.

```csharp
public sealed record CreateStaffCommand : IRequest<StaffMemberDto>
{
    public required string Name { get; init; }
    // ...
}

public sealed class CreateStaffCommandValidator : AbstractValidator<CreateStaffCommand> { ... }

public sealed class CreateStaffCommandHandler(
    IStaffMemberRepository staffRepo,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork
) : IRequestHandler<CreateStaffCommand, StaffMemberDto> { ... }
```

### Queries

Single file containing: `record` (positional parameters, implements `IRequest<T>` + `IQuery` marker) + `Handler`.

- No validator. No `IUnitOfWork`.
- `IQuery` marker interface lives in `Chronith.Application.Behaviors.PerformanceBehavior`.

```csharp
public sealed record GetStaffQuery(Guid Id) : IRequest<StaffMemberDto>, IQuery;
```

### DTOs

- `sealed record` with positional parameters.

```csharp
public sealed record StaffMemberDto(Guid Id, string Name, bool IsActive, ...);
```

### Mappers

- Static class with extension methods on domain models.

```csharp
public static class StaffMemberMapper
{
    public static StaffMemberDto ToDto(this StaffMember staff) => new(...);
}
```

---

## 5. Infrastructure Patterns

### Entities

- `sealed` POCO classes in `Chronith.Infrastructure.Persistence.Entities`.
- Properties: `Guid Id`, `Guid TenantId`, `bool IsDeleted`, plus domain-specific fields.
- Flat structure — no navigation properties on the entity itself (use EF `Include()` in repositories).

### EF Configurations

- `sealed` class implementing `IEntityTypeConfiguration<T>`.
- Table names in `snake_case`, schema `"chronith"`.
- Enums stored as strings.
- Row version (`xmin` on PostgreSQL) for optimistic concurrency.
- Global query filters for tenant isolation (`TenantId == tenantContext.TenantId`) and soft delete (`!IsDeleted`).

### Entity Mappers

Static class with two methods:

| Method       | Technique                                                                                                          |
| ------------ | ------------------------------------------------------------------------------------------------------------------ |
| `ToDomain()` | Uses `SetProperty` reflection helper for private setters, `SetBackingField` for private collection backing fields. |
| `ToEntity()` | Plain object initializer.                                                                                          |

### Repositories

- Constructor-injected `ChronithDbContext`.
- `AsNoTracking()` on all read queries.
- `Include()` for navigation properties.
- `ExecuteDeleteAsync` + `AddRangeAsync` for replacing child collections on update.
- Cross-tenant queries (background services, public endpoints) use `.IgnoreQueryFilters()` + explicit `!IsDeleted` filter.

### Background Services

- Extend `BackgroundService`.
- Primary constructor injection: `IServiceScopeFactory`, `IOptions<T>`, `ILogger<T>`.
- `ExecuteAsync` loops with `while (!stoppingToken.IsCancellationRequested)`, catches non-cancellation exceptions, delays between iterations.
- Options classes are `sealed` with simple properties and defaults, in `Chronith.Infrastructure.Services` namespace.

---

## 6. Testing Strategy

### TDD Discipline

**Strict TDD: write a failing test first, then implement, then commit.** This is not optional.

### Test Layers

| Layer       | Project                      | Requires                | Key Packages                                                                         |
| ----------- | ---------------------------- | ----------------------- | ------------------------------------------------------------------------------------ |
| Unit        | `Chronith.Tests.Unit`        | Nothing                 | xUnit, FluentAssertions, NSubstitute                                                 |
| Integration | `Chronith.Tests.Integration` | Docker (Testcontainers) | xUnit, FluentAssertions, Testcontainers.PostgreSql, Testcontainers.Redis             |
| Functional  | `Chronith.Tests.Functional`  | Docker (Testcontainers) | xUnit, FluentAssertions, Microsoft.AspNetCore.Mvc.Testing, Testcontainers.PostgreSql |
| Performance | `Chronith.Tests.Performance` | Nothing                 | BenchmarkDotNet (console exe, not xUnit)                                             |
| Load        | `tests/Chronith.Tests.Load/` | Running stack + k6      | k6 JavaScript scripts                                                                |

### Test Builders

- **Domain-safe builders** (fluent instance pattern): `new BookingBuilder().InStatus(status).Build()`.
- **Infrastructure builders** (static factory + reflection): `BookingTypeBuilder.BuildTimeSlot(...)`.

### Functional Test Patterns

| Concept                      | Location                               | Description                                                                                             |
| ---------------------------- | -------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| `FunctionalTestFixture`      | `tests/.../Fixtures/`                  | Testcontainers PostgreSQL, runs migrations, provides `CreateClient(role)` and `CreateAnonymousClient()` |
| `[Collection("Functional")]` | Every functional test class            | Shared fixture via xUnit collection                                                                     |
| `SeedData`                   | `tests/.../Helpers/SeedData.cs`        | Static methods: `SeedTenantAsync()`, `SeedBookingTypeAsync()`, `SeedBookingAsync()`, etc.               |
| `TestConstants`              | `tests/.../Fixtures/TestConstants.cs`  | `TenantId`, `AdminUserId`, `StaffUserId`, `CustomerUserId`                                              |
| `TestJwtFactory`             | `tests/.../Fixtures/TestJwtFactory.cs` | `CreateToken(role, userId, tenantId?)`                                                                  |

Each test class has its own `BookingTypeSlug` constant and private `EnsureSeedAsync()` method.
Two test files per feature: `*EndpointsTests.cs` (happy paths) and `*AuthTests.cs` (role-based access).

### FluentAssertions Notes

- Use `BeGreaterThanOrEqualTo` (NOT `BeGreaterOrEqualTo`).

---

## 7. Git & Branching Model

### Sensitive Data in Commits

**Never commit real secrets.** Before staging any file, verify it contains no plaintext credentials.

What counts as sensitive: JWT signing keys, API keys (PayMongo, Twilio, Firebase), database connection strings with real passwords, SMTP credentials, webhook secrets, AES encryption keys, OAuth tokens, bearer tokens, and any value sourced from Key Vault or a secrets manager.

**Placeholder conventions — stay consistent with what is already in the codebase:**

| Field type                   | Placeholder                                     |
| ---------------------------- | ----------------------------------------------- |
| JWT / HMAC signing key       | `REPLACE_WITH_SECRET__run_openssl_rand_-hex_32` |
| AES encryption key versions  | `SET_VIA_AZURE_APP_SERVICE_OR_ENV`              |
| Payment provider credentials | `""` (empty string)                             |
| Callback / redirect URLs     | `https://example.com/...`                       |
| k6 / load-test signing keys  | `change-me-in-production-at-least-32-chars`     |
| HMAC zero-value placeholder  | `AAAA...=` (base64-encoded zero bytes)          |

If a real secret is already staged, un-stage it and rotate the credential immediately. Do not attempt to rewrite history — rotate first, redact second.

---

### Conventional Commits

All commits must use conventional commit format with scope:

```
feat(domain): add staff member model
feat(api): add public booking endpoints
fix: resolve query filter issue for public endpoints
test: add functional tests for analytics
refactor(infra): extract notification channel factory
docs: update AGENTS.md
```

### Branching Strategy (GitHub Flow)

```
main                    ← only long-lived branch; always deployable
 ├── feat/<short-desc>  ← new feature, created from main
 ├── fix/<short-desc>   ← bug fix, created from main
 └── docs/<short-desc>  ← documentation only, created from main
```

- `main` is always deployable.
- Create a branch from `main`. Open a PR targeting `main`. Merge when CI is green and review is approved.
- No `develop` branch. No parent feature branches. No `task-N` sub-branches.
- Keep branches short-lived — prefer small, focused PRs.

### Tags

Tag releases on `main` after merge: `v0.1.0`, `v0.2.0`, ..., `v1.0.0`.

---

## 8. CI/CD & PR Lifecycle

### CI Pipeline

Multiple workflow files run on pushes to `main` and on PRs targeting `main`:

**`ci.yml` — API (runs on API/infra path changes)**

| Job              | Description                                                                                                  |
| ---------------- | ------------------------------------------------------------------------------------------------------------ |
| `changes`        | Detect changed paths (dorny/paths-filter); gates downstream jobs.                                            |
| `dotnet-test`    | Postgres 17 + Redis 8 service containers. Builds Release, runs unit/integration/functional tests separately. |
| `docker-build`   | Multi-stage Docker build with Buildx + GHA cache.                                                            |
| `playwright-e2e` | Builds API + dashboard images, starts compose stack, runs Playwright E2E suite.                              |
| `benchmarks`     | BenchmarkDotNet (push to `main` only).                                                                       |
| `codeql`         | CodeQL `security-and-quality` for `[csharp, javascript]`.                                                    |
| `secret-scan`    | Scans for accidentally committed secrets.                                                                    |
| `deploy-api`     | Deploys API to Azure App Service (`chronith-api`, `rg-chronith`, `southeastasia`) on push to `main`.         |

**`dashboard-ci.yml` — Admin Dashboard**

| Job                | Description                                      |
| ------------------ | ------------------------------------------------ |
| `lint-typecheck`   | ESLint + `tsc --noEmit`.                         |
| `unit-tests`       | Vitest (component + hook tests).                 |
| `build`            | `next build` — verify production build succeeds. |
| `docker-build`     | Build dashboard Docker image.                    |
| `deploy-dashboard` | Deploys dashboard to Azure on push to `main`.    |

**`sdk-ci.yml` — TypeScript SDK** — lint + type-check + build.

**`sdk-csharp-ci.yml` — C# SDK** — build + test + NuGet publish.

**`docs-ci.yml` — Documentation site** — Starlight build + GitHub Pages deploy.

**`docs-gate.yml` — Docs-only PRs** — posts a synthetic pass status so docs-only PRs are not blocked by skipped API jobs.

---

## 9. Version History

### Completed Versions (all tagged on `main`)

| Version | Tag      | Summary                                                                                                                                                                     |
| ------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| v0.1    | —        | Core domain models, booking CRUD, tenant isolation                                                                                                                          |
| v0.2    | `v0.2.0` | Availability engine, slot conflict detection                                                                                                                                |
| v0.3    | —        | Webhook outbox, status change notifications                                                                                                                                 |
| v0.4    | `v0.4.0` | JWT + API key auth, Redis caching, customer callbacks, rate limiting                                                                                                        |
| v0.5    | `v0.5.0` | Payment/pricing integration, PayMongo provider, free booking flow                                                                                                           |
| v0.6    | —        | Staff management, lifecycle enhancements (reschedule, waitlist, time blocks, custom fields), notifications (email/SMS/push), analytics, public booking endpoints, iCal feed |
| v0.7    | `v0.7.0` | API versioning (`/v1/`), customer accounts (built-in + OIDC), recurring bookings, idempotency keys                                                                          |
| v0.8    | `v0.8.0` | Audit logging, OpenTelemetry observability, security hardening, database optimization, notification templates                                                               |
| v0.8.2  | `v0.8.2` | Magic link auth, OTel spans + metrics, API key aging service                                                                                                                |
| v0.9    | `v0.9.0` | TypeScript SDK, Next.js admin dashboard, C# SDK + NuGet, Starlight docs, Playwright E2E                                                                                     |
| v1.0    | `v1.0.0` | GA: complete dashboard, public booking page, security audit, k6 load tests, CHANGELOG                                                                                       |

### Post-v1.0 (main, untagged)

Work merged to `main` after the v1.0.0 GA tag:

| Feature area               | PRs           | Description                                                                                                                           |
| -------------------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| Deployment infrastructure  | #20–#29       | Azure App Service (live at `chronith-api.azurewebsites.net`), Fly.io config, Podman local dev, GitHub Flow migration, CI path filters |
| Per-tenant payment config  | #32           | Full CRUD for `TenantPaymentConfig` with AES-256-GCM encryption of provider credentials                                               |
| Security hardening         | #37, #41, #42 | Webhook secret encryption, Argon2id password hashing, AES-256-GCM PII field encryption                                                |
| API key scopes (RBAC)      | #44–#48       | Scope-based authorization on all API key-authenticated endpoints                                                                      |
| Per-tenant payment webhook | #49           | PayMongo inbound webhook route scoped per tenant slug                                                                                 |
| Public booking status      | #51           | Anonymous `GET /v1/public/{tenantSlug}/bookings/{id}` endpoint                                                                        |

Implementation plans live in `docs/plans/` following the naming pattern:
`YYYY-MM-DD-chronith-v{X.Y}-plan.md`

---

## 10. Key Commands

```bash
# Build
dotnet build Chronith.slnx

# Run all tests
dotnet test Chronith.slnx

# Unit tests only (no Docker needed)
dotnet test tests/Chronith.Tests.Unit

# Integration tests (requires Docker for Testcontainers)
dotnet test tests/Chronith.Tests.Integration

# Functional tests (requires Docker for Testcontainers)
dotnet test tests/Chronith.Tests.Functional

# Benchmarks
dotnet run -c Release --project tests/Chronith.Tests.Performance -- --filter "*"

# k6 load tests (requires running stack)
podman compose up -d
k6 run tests/Chronith.Tests.Load/scripts/availability.js \
  --env BASE_URL=http://localhost:5001 \
  --env JWT_SIGNING_KEY=change-me-in-production-at-least-32-chars

# Podman
# Requires Podman Desktop with Docker Compatibility enabled (Settings → Resources → Podman Compose Setup)
# This provides /var/run/docker.sock — Testcontainers and act auto-discover it
podman compose up -d          # Start full stack
podman compose down            # Stop

# EF migrations
dotnet ef migrations add <Name> \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API \
  --output-dir Migrations/PostgreSQL

# Validate CI locally before PR
act pull_request --workflows .github/workflows/ci.yml

# Check PR review comments
gh api repos/{owner}/{repo}/pulls/{number}/comments

# Deploy to Azure App Service (F1 free tier, southeastasia)
# https://chronith-api.azurewebsites.net
dotnet publish src/Chronith.API/Chronith.API.csproj -c Release -o ./azure-publish
cd azure-publish && zip -r ../chronith-deploy.zip . && cd ..
az webapp deploy --name chronith-api --resource-group rg-chronith --src-path chronith-deploy.zip --type zip

# Tail live logs
az webapp log tail --name chronith-api --resource-group rg-chronith

# Update a secret / app setting
az webapp config appsettings set --name chronith-api --resource-group rg-chronith --settings Key="Value"

# Restart the app
az webapp restart --name chronith-api --resource-group rg-chronith

git checkout main && git pull && git branch -d {branch-name}
```

---

## 11. Configuration Notes

### EncryptionKey

`Security:EncryptionKey` in `appsettings.json` must be a **Base64-encoded 32-byte (256-bit) key** used for AES-256-GCM encryption of sensitive notification settings.

The placeholder value in `appsettings.json` (`AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=`) is **not safe for production**. Generate a new key before deploying:

```bash
# Generate a cryptographically random 32-byte key and base64-encode it
openssl rand -base64 32
```

Set the result as `Security:EncryptionKey` via an environment variable or secrets manager — never commit a real key to source control.

```

```
