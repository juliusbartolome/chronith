# Chronith Test Plan

## Scope & Goals

This document describes the testing strategy for Chronith v0.1 — a multi-tenant, time-slot and calendar booking engine
built with .NET 10 Clean Architecture. The goal is confidence at every layer: domain correctness, infrastructure
contracts, HTTP behaviour, algorithmic performance, and production-load headroom.

---

## Test Layers

### 1. Unit Tests — `tests/Chronith.Tests.Unit`

**Purpose:** Verify pure domain logic and application services in isolation, with no I/O.

**Toolchain:** xUnit · FluentAssertions · NSubstitute

**Run:**

```bash
dotnet test Chronith.slnx --filter "FullyQualifiedName~Unit"
```

**Coverage areas:**

- `SlotGeneratorService` — time-slot generation for `TimeSlotBookingType`
- `BookingType` domain models — `Create()` factory validation, availability-window rules
- `Booking` aggregate — lifecycle state machine (PendingPayment → PendingVerification → Confirmed, Cancel from any state)
- Value objects and guard clauses

**Current count:** 56 tests / 56 passing

---

### 2. Integration Tests — `tests/Chronith.Tests.Integration`

**Purpose:** Verify that EF Core mappings, migrations, and repository queries behave correctly against a real Postgres
instance spun up via Testcontainers.

**Toolchain:** xUnit · FluentAssertions · EF Core · Testcontainers (`postgres:17-alpine`)

**Run:**

```bash
dotnet test Chronith.slnx --filter "FullyQualifiedName~Integration"
```

**Coverage areas:**

- Repository CRUD (BookingType, Booking, Tenant)
- `AsNoTracking` read queries
- `xmin`-based optimistic concurrency (`BookingEntity.RowVersion`)
- Multi-tenant data isolation (tenant-scoped queries never leak across tenants)
- EF migration idempotency

**Current count:** 21 tests / 21 passing

---

### 3. Functional Tests — `tests/Chronith.Tests.Functional`

**Purpose:** End-to-end HTTP tests against a real ASP.NET Core host with Testcontainers Postgres. Exercises the full
request pipeline including FastEndpoints routing, FluentValidation, JWT authentication, and EF Core.

**Toolchain:** xUnit · FluentAssertions · `WebApplicationFactory<Program>` · Testcontainers · HttpClient

**Run:**

```bash
dotnet test Chronith.slnx --filter "FullyQualifiedName~Functional"
```

**Coverage areas:**

- Booking type CRUD (create time-slot type, create calendar type, get, list, update, delete)
- Availability query (returns slots for a date range)
- Booking lifecycle (create → pay → confirm → cancel)
- Payment webhook handling
- Webhook registration and delivery
- Tenant management

#### Auth Matrix

| Endpoint group              | No token | Wrong tenant token | Admin token | Customer token |
| --------------------------- | -------- | ------------------ | ----------- | -------------- |
| POST /booking-types         | 401      | 403                | 201         | 403            |
| PUT /booking-types          | 401      | 403                | 200         | 403            |
| DELETE /booking-types       | 401      | 403                | 204         | 403            |
| GET /booking-types          | 200      | 200                | 200         | 200            |
| GET /availability           | 200      | 200                | 200         | 200            |
| POST /bookings              | 401      | 403                | 201         | 201            |
| GET /bookings/{id}          | 401      | 403                | 200         | 200 (own only) |
| POST /bookings/{id}/pay     | 401      | 403                | 200         | 200 (own only) |
| POST /bookings/{id}/confirm | 401      | 403                | 200         | 403            |
| POST /bookings/{id}/cancel  | 401      | 403                | 200         | 200 (own only) |
| POST /webhooks              | 401      | 403                | 201         | 403            |
| POST /payment-webhook       | —        | 401 (bad sig)      | —           | —              |

**Current count:** 89 tests / 89 passing

---

### 4. Performance Tests — `tests/Chronith.Tests.Performance`

**Purpose:** Micro-benchmark hot paths with BenchmarkDotNet to catch algorithmic regressions before they reach
production.

**Toolchain:** BenchmarkDotNet (Release mode, no debugger)

**Run (dry-run for CI verification):**

```bash
dotnet run --project tests/Chronith.Tests.Performance -c Release -- --job dry --filter '*'
```

**Run (full benchmark):**

```bash
dotnet run --project tests/Chronith.Tests.Performance -c Release -- --filter '*'
```

**Benchmarks:**

| Benchmark                                      | Method                                              | Expected magnitude |
| ---------------------------------------------- | --------------------------------------------------- | ------------------ |
| `SlotGenerationBenchmarks.GenerateDailySlots`  | `SlotGeneratorService.GenerateSlots` — 1 day window | < 1 ms             |
| `SlotGenerationBenchmarks.GenerateWeeklySlots` | `SlotGeneratorService.GenerateSlots` — 7 day window | < 5 ms             |
| `ConflictRangeBenchmarks.NoConflict`           | Conflict-range query against empty list             | < 100 ns           |
| `ConflictRangeBenchmarks.WithConflicts`        | Conflict-range query against 100-element list       | < 1 µs             |

---

### 5. Load Tests — `tests/Chronith.Tests.Load`

**Purpose:** Validate that the deployed API sustains realistic concurrent traffic within latency SLOs.

**Toolchain:** k6 (grafana/k6)

**Requires:** A running Chronith API (`docker compose up` or direct `dotnet run`). See `tests/Chronith.Tests.Load/README.md`
for full setup instructions including seed data and environment variables.

**Run individual script:**

```bash
k6 run -e BASE_URL=http://localhost:5000 \
       -e TENANT_ID=<uuid> \
       -e BOOKING_TYPE_ID=<uuid> \
       -e JWT_SECRET=change-me-in-production-at-least-32-chars \
       tests/Chronith.Tests.Load/scripts/availability.js
```

#### Scripts & SLOs

| Script                  | Scenario                                 | VUs | Duration | SLO                           |
| ----------------------- | ---------------------------------------- | --- | -------- | ----------------------------- |
| `availability.js`       | GET /availability — read-heavy           | 100 | 60 s     | p95 < 100 ms, error rate < 1% |
| `create-booking.js`     | POST /bookings — write load              | 50  | 30 s     | p95 < 200 ms, error rate < 1% |
| `concurrent-booking.js` | 50 VUs race for a single capacity-1 slot | 50  | 10 s     | exactly 1 success, rest 409   |
| `booking-lifecycle.js`  | Full create → pay → confirm per VU       | 20  | 30 s     | p95 < 500 ms, error rate < 1% |

---

## Running All Automated Tests

```bash
# Unit + Integration + Functional (166 tests total)
dotnet test Chronith.slnx

# With verbose output
dotnet test Chronith.slnx --logger "console;verbosity=normal"
```

---

## Performance Targets (summary)

| Layer                     | Target                                         |
| ------------------------- | ---------------------------------------------- |
| Unit tests                | < 5 s total                                    |
| Integration tests         | < 30 s total (Testcontainers startup included) |
| Functional tests          | < 60 s total                                   |
| Slot generation (1 day)   | < 1 ms                                         |
| Slot generation (7 days)  | < 5 ms                                         |
| Availability endpoint p95 | < 100 ms @ 100 VUs                             |
| Create booking p95        | < 200 ms @ 50 VUs                              |
| Full lifecycle p95        | < 500 ms @ 20 VUs                              |

---

## Known Gaps

- **Load tests require a live server** — they are not run in `dotnet test` and need manual invocation or a dedicated CI
  job with a running Docker Compose stack.
- **No mutation testing** — Stryker.NET not yet configured.
- **No contract tests** — Consumer-driven contract testing (Pact) is not implemented for this version.
- **Performance benchmarks exclude I/O** — BenchmarkDotNet benchmarks cover in-process logic only; database-level
  performance is covered by load tests.
- **Load test seed data is manual** — scripts require `TENANT_ID` and `BOOKING_TYPE_ID` to be pre-seeded; no automated
  fixture setup yet.
