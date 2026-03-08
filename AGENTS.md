# AGENTS.md — Chronith

This file is the single source of truth for any AI agent working on this codebase.
Read it fully before making any changes.

---

## 1. Project Overview

**Chronith** is a multi-tenant booking engine API built in .NET 10 using clean architecture.

| Concern         | Technology                                                                |
| --------------- | ------------------------------------------------------------------------- |
| Web framework   | FastEndpoints 8.x                                                         |
| CQRS / Mediator | MediatR 14.x                                                              |
| Validation      | FluentValidation 12.x                                                     |
| ORM             | EF Core 10.x + Npgsql                                                     |
| Database        | PostgreSQL 17                                                             |
| Cache           | Redis 8 (StackExchange.Redis)                                             |
| Auth            | JWT (HMAC symmetric) + API Key (`X-Api-Key`)                              |
| Logging         | Serilog (console sink)                                                    |
| Notifications   | MailKit (SMTP), Twilio (SMS), FirebaseAdmin (push)                        |
| Payments        | Pluggable `IPaymentProvider` (Stub / PayMongo)                            |
| Testing         | xUnit, FluentAssertions, NSubstitute, Testcontainers, BenchmarkDotNet, k6 |
| CI              | GitHub Actions (6 jobs), CodeQL                                           |
| Container       | Docker multi-stage, Docker Compose                                        |

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

### Branching Strategy

```
main
 └── develop
      └── feat/v{X.Y}-{feature-name}          (parent feature branch)
           ├── feat/v{X.Y}/task-{N}-{name}     (sub-branch per task)
           ├── feat/v{X.Y}/task-{N+1}-{name}
           └── ...
```

- **Sub-branches** are created from the parent feature branch for each task.
- Sub-branches are merged back into the parent with `--no-ff`.
- Parent feature branches are merged into `develop` via PR.
- **Only `develop` may merge into `main`** — enforced by CI (`check-target-branch` job).

### Tags

Tag releases on `develop` after merge: `v0.1.0`, `v0.2.0`, ..., `v0.5.0`.

---

## 8. CI/CD & PR Lifecycle

### CI Pipeline (`.github/workflows/ci.yml`)

6 jobs run on every push to `main`/`develop` and on PRs targeting them:

| Job                   | Description                                                                                                  |
| --------------------- | ------------------------------------------------------------------------------------------------------------ |
| `dotnet-test`         | Postgres 17 + Redis 8 service containers. Builds Release, runs unit/integration/functional tests separately. |
| `docker-build`        | Multi-stage Docker build with Buildx + GHA cache.                                                            |
| `k6-load-tests`       | Builds image, starts docker-compose stack, seeds data, runs 4 k6 scripts.                                    |
| `benchmarks`          | BenchmarkDotNet (push to `main`/`develop` only).                                                             |
| `check-target-branch` | Enforces only `develop` → `main` merges.                                                                     |
| `codeql`              | CodeQL `security-and-quality` for `[csharp, javascript]`.                                                    |

### PR Lifecycle — MANDATORY

Agents MUST follow this lifecycle for every PR. No exceptions.

#### Step 1: Pre-PR Validation

Before creating any PR, validate the full CI workflow locally using [act](https://github.com/nektos/act):

```bash
act pull_request --workflows .github/workflows/ci.yml
```

All jobs must pass locally: `dotnet-test`, `docker-build`, `codeql`, `k6-load-tests`.
Do NOT create a PR until act confirms all jobs pass.

#### Step 2: Create & Push PR

```bash
gh pr create --title "feat: ..." --body "$(cat <<'EOF'
## Summary
- ...
EOF
)"
```

#### Step 3: Post-PR Review Loop

After pushing the PR, the agent MUST monitor for and resolve review feedback:

1. **Poll for review comments** using `gh` CLI:

   ```bash
   # Check PR review comments
   gh api repos/{owner}/{repo}/pulls/{number}/comments

   # Check PR reviews
   gh pr view {number} --json reviews

   # Check PR status and checks
   gh pr checks {number}
   ```

2. **Address each comment** — fix code, refactor, or explain the reasoning.

3. **Reply on GitHub** confirming resolution:

   ```bash
   gh api repos/{owner}/{repo}/pulls/{number}/comments/{comment_id}/replies \
     -f body="Fixed in <commit-sha>. <brief explanation>"
   ```

4. **Re-validate full CI locally using `act`** before pushing the fixes. Do NOT push without re-running act.

5. **Push the fixes.**

6. **Repeat steps 1–5** until:
   - All CI checks are green
   - All review comments are resolved
   - No outstanding change requests

#### Step 4: Merge

Only merge once all checks pass and all reviews are resolved.

```bash
gh pr merge {number} --merge --delete-branch
```

After merge, clean up locally:

```bash
git checkout develop && git pull && git branch -d {branch-name}
```

---

## 9. Version History & Roadmap

### Completed Versions

| Version | Tag      | Summary                                                                                                                                                                     |
| ------- | -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| v0.1    | —        | Core domain models, booking CRUD, tenant isolation                                                                                                                          |
| v0.2    | `v0.2.0` | Availability engine, slot conflict detection                                                                                                                                |
| v0.3    | —        | Webhook outbox, status change notifications                                                                                                                                 |
| v0.4    | `v0.4.0` | JWT + API key auth, Redis caching, customer callbacks, rate limiting                                                                                                        |
| v0.5    | `v0.5.0` | Payment/pricing integration, PayMongo provider, free booking flow                                                                                                           |
| v0.6    | —        | Staff management, lifecycle enhancements (reschedule, waitlist, time blocks, custom fields), notifications (email/SMS/push), analytics, public booking endpoints, iCal feed |

### Upcoming — Design Docs

| Version | Design Doc                                      | Focus                  |
| ------- | ----------------------------------------------- | ---------------------- |
| v0.7    | `docs/plans/2026-03-07-chronith-v0.7-design.md` | Next planned milestone |
| v0.8    | `docs/plans/2026-03-07-chronith-v0.8-design.md` | —                      |
| v0.9    | `docs/plans/2026-03-07-chronith-v0.9-design.md` | —                      |
| v1.0    | `docs/plans/2026-03-07-chronith-v1.0-design.md` | Production release     |

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
docker compose up -d
k6 run tests/Chronith.Tests.Load/scripts/availability.js \
  --env BASE_URL=http://localhost:5001 \
  --env JWT_SIGNING_KEY=change-me-in-production-at-least-32-chars

# Docker
docker compose up -d          # Start full stack
docker compose down            # Stop

# EF migrations
dotnet ef migrations add <Name> \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API \
  --output-dir Migrations/PostgreSQL

# Validate CI locally before PR
act pull_request --workflows .github/workflows/ci.yml

# Check PR review comments
gh api repos/{owner}/{repo}/pulls/{number}/comments
```
