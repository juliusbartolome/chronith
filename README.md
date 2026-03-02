# Chronith

A multi-tenant booking engine API built with .NET 10. Supports two booking models — fixed-duration time slots and whole-day calendar blocks — with a built-in payment and state machine workflow.

## Table of Contents

- [Features](#features)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [API Reference](#api-reference)
- [Authentication](#authentication)
- [Booking State Machine](#booking-state-machine)
- [Running Tests](#running-tests)
- [Load Tests](#load-tests)
- [Benchmarks](#benchmarks)
- [CI/CD](#cicd)
- [Project Structure](#project-structure)

## Features

- **Multi-tenancy** — each tenant is fully isolated; all resources are scoped via JWT claims (no tenant ID in URL paths)
- **Two booking kinds**
  - `Fixed` — time slot with configurable duration, buffer before/after, and weekly availability windows
  - `Calendar` — whole-day block bookings
- **Booking state machine** — `PendingPayment → PendingVerification → Confirmed`, cancellable from any non-terminal state
- **Webhook subscriptions** — per-booking-type outbound webhooks for booking lifecycle events
- **Real webhook delivery** — outbox pattern with background dispatcher, exponential back-off (30s, 2m, 10m, 1h, 4h), up to 6 attempts
- **Payment provider abstraction** — pluggable `IPaymentProvider` with stub (default) and PayMongo implementations
- **API key authentication** — `X-Api-Key` header alongside JWT Bearer; keys use `cth_` prefix, SHA-256 hash stored
- **Payment integration** — dedicated `TenantPaymentService` role and a payment webhook receiver endpoint
- **Optimistic concurrency** — row-level via PostgreSQL `xmin`
- **Clean Architecture** — Domain / Application (MediatR CQRS) / Infrastructure / API layers
- **Health probes** — separate liveness and readiness endpoints for container orchestration

## Architecture

```
src/
├── Chronith.API            ← FastEndpoints, JWT auth, health checks, middleware
├── Chronith.Application    ← MediatR commands/queries, DTOs, validators
├── Chronith.Domain         ← Entities, value objects, domain events, state machine
└── Chronith.Infrastructure ← EF Core + Npgsql, migrations, repositories, outbox
```

All database tables are in the `chronith` PostgreSQL schema. The only supported database in v0.1 is PostgreSQL.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`10.0.100` or later — see `global.json`)
- [Docker](https://docs.docker.com/get-docker/) and Docker Compose (for local development and load tests)

## Getting Started

**Run with Docker Compose:**

```bash
docker compose up
```

The API is available at `http://localhost:5001`. PostgreSQL is exposed on `localhost:5432`.

**Run without Docker:**

Start a PostgreSQL instance and set the connection string, then:

```bash
dotnet run --project src/Chronith.API
```

The application applies EF Core migrations on startup. No manual migration step is required.

## Configuration

| Key                                   | Default                                     | Description                                                |
| ------------------------------------- | ------------------------------------------- | ---------------------------------------------------------- |
| `ConnectionStrings:DefaultConnection` | see `docker-compose.yml`                    | PostgreSQL connection string                               |
| `Jwt:SigningKey`                      | `change-me-in-production-at-least-32-chars` | Symmetric HMAC signing key — **change this in production** |
| `Jwt:Issuer`                          | `chronith`                                  | JWT issuer claim                                           |
| `Jwt:Audience`                        | `chronith`                                  | JWT audience claim                                         |
| `Payments:Provider`                   | `Stub`                                      | Payment provider: `Stub` or `PayMongo`                     |
| `Payments:Currency`                   | `PHP`                                       | Currency code for payment intents                          |
| `Payments:PayMongo:SecretKey`         | _(none)_                                    | PayMongo secret key (required when provider is `PayMongo`) |
| `Payments:PayMongo:WebhookSecret`     | _(none)_                                    | PayMongo webhook signing secret                            |
| `Webhooks:DispatchIntervalSeconds`    | `10`                                        | Outbox dispatcher poll interval                            |
| `Webhooks:HttpTimeoutSeconds`         | `10`                                        | HTTP timeout for webhook delivery                          |

Override any value via environment variables using the `__` separator (e.g., `Jwt__SigningKey=...`).

## API Reference

All endpoints require a valid JWT Bearer token unless noted otherwise.

### Health Checks _(unauthenticated)_

| Method | Route           | Description                                                  |
| ------ | --------------- | ------------------------------------------------------------ |
| `GET`  | `/health/live`  | Liveness probe — returns `Healthy` if the process is running |
| `GET`  | `/health/ready` | Readiness probe — verifies the database connection           |

### Tenant

| Method | Route     | Roles         | Description                          |
| ------ | --------- | ------------- | ------------------------------------ |
| `GET`  | `/tenant` | `TenantAdmin` | Returns the current tenant's profile |

### Booking Types

| Method   | Route                   | Roles                                    | Description                           |
| -------- | ----------------------- | ---------------------------------------- | ------------------------------------- |
| `GET`    | `/booking-types`        | `TenantAdmin`, `TenantStaff`, `Customer` | List all booking types for the tenant |
| `POST`   | `/booking-types`        | `TenantAdmin`                            | Create a new booking type             |
| `GET`    | `/booking-types/{slug}` | `TenantAdmin`, `TenantStaff`, `Customer` | Get a single booking type by slug     |
| `PUT`    | `/booking-types/{slug}` | `TenantAdmin`                            | Update a booking type                 |
| `DELETE` | `/booking-types/{slug}` | `TenantAdmin`                            | Delete a booking type                 |

### Availability

| Method | Route                                          | Roles                                    | Description                               |
| ------ | ---------------------------------------------- | ---------------------------------------- | ----------------------------------------- |
| `GET`  | `/booking-types/{slug}/availability?from=&to=` | `TenantAdmin`, `TenantStaff`, `Customer` | List available slots in a date/time range |

### Bookings

| Method | Route                            | Roles                                                | Description                          |
| ------ | -------------------------------- | ---------------------------------------------------- | ------------------------------------ |
| `POST` | `/booking-types/{slug}/bookings` | `TenantAdmin`, `TenantStaff`, `Customer`             | Create a booking                     |
| `GET`  | `/booking-types/{slug}/bookings` | `TenantAdmin`, `TenantStaff`                         | List bookings for a type (paginated) |
| `GET`  | `/bookings/{bookingId}`          | `TenantAdmin`, `TenantStaff`, `Customer`             | Get a single booking                 |
| `POST` | `/bookings/{bookingId}/confirm`  | `TenantAdmin`, `TenantStaff`                         | Confirm a booking                    |
| `POST` | `/bookings/{bookingId}/cancel`   | `TenantAdmin`, `TenantStaff`, `Customer`             | Cancel a booking                     |
| `POST` | `/bookings/{bookingId}/pay`      | `TenantAdmin`, `TenantStaff`, `TenantPaymentService` | Mark a booking as paid               |

### Webhooks

| Method   | Route                                        | Roles         | Description                           |
| -------- | -------------------------------------------- | ------------- | ------------------------------------- |
| `POST`   | `/booking-types/{slug}/webhooks`             | `TenantAdmin` | Register a webhook for a booking type |
| `GET`    | `/booking-types/{slug}/webhooks`             | `TenantAdmin` | List webhooks for a booking type      |
| `DELETE` | `/booking-types/{slug}/webhooks/{webhookId}` | `TenantAdmin` | Delete a webhook subscription         |

### API Keys

| Method   | Route                   | Roles         | Description                          |
| -------- | ----------------------- | ------------- | ------------------------------------ |
| `POST`   | `/tenant/api-keys`      | `TenantAdmin` | Create a new API key (returns raw key once) |
| `GET`    | `/tenant/api-keys`      | `TenantAdmin` | List all API keys for the tenant     |
| `DELETE` | `/tenant/api-keys/{id}` | `TenantAdmin` | Revoke an API key                    |

### Payments (Inbound Webhook Receiver)

| Method | Route               | Roles                  | Description                                              |
| ------ | ------------------- | ---------------------- | -------------------------------------------------------- |
| `POST` | `/webhooks/payment` | `TenantPaymentService` | Receive a payment confirmation from an external provider |

**Error responses** are [RFC 7807 Problem Details](https://datatracker.ietf.org/doc/html/rfc7807) JSON objects.

## Authentication

The API uses JWT Bearer authentication with a symmetric HMAC signing key.

**Required claims:**

| Claim       | Description                                                             |
| ----------- | ----------------------------------------------------------------------- |
| `tenant_id` | GUID of the tenant — all resources are scoped to this value             |
| `role`      | One of `TenantAdmin`, `TenantStaff`, `Customer`, `TenantPaymentService` |

Example token payload:

```json
{
  "sub": "user-guid",
  "tenant_id": "00000000-0000-0000-0000-000000000001",
  "role": "TenantAdmin",
  "iss": "chronith",
  "aud": "chronith"
}
```

### API Key Authentication

Endpoints that accept `ApiKey` scheme (currently `GET /tenant/api-keys`) also accept an `X-Api-Key` header:

```
X-Api-Key: cth_<base64url-encoded-32-random-bytes>
```

API keys are created via `POST /tenant/api-keys` and are scoped to a role. The raw key is returned once on creation and never stored — only its SHA-256 hash is persisted. Keys with `TenantAdmin` role can access all `TenantAdmin`-protected endpoints that accept the `ApiKey` scheme.

## Booking State Machine

```
                    ┌──────────────────┐
                    │  PendingPayment  │──────────┐
                    └────────┬─────────┘          │
                             │ Pay                │
                             ▼                    │
               ┌───────────────────────┐          │ Cancel
               │  PendingVerification  │          │
               └──────────┬────────────┘          │
                          │ Confirm               │
                          ▼                       │
                   ┌────────────┐                 │
                   │ Confirmed  │─────────────────┤
                   └────────────┘                 │
                                                  ▼
                                           ┌──────────┐
                                           │Cancelled │
                                           └──────────┘
```

- Bookings start in `PendingPayment`.
- A `PaymentMode` of `Manual` skips the payment step — the booking moves directly to `PendingVerification`.
- `Cancel` is available from `PendingPayment`, `PendingVerification`, and `Confirmed`.

## Running Tests

```bash
# All tests (requires a running PostgreSQL instance)
dotnet test Chronith.slnx

# Unit tests only (no database required)
dotnet test tests/Chronith.Tests.Unit

# Integration tests only
dotnet test tests/Chronith.Tests.Integration

# Functional tests only
dotnet test tests/Chronith.Tests.Functional
```

Integration and functional tests use [Testcontainers](https://testcontainers.com/) to spin up a PostgreSQL container automatically. In CI, they connect to a pre-provisioned service container instead.

**Test counts:** 107 unit · 24 integration · 98 functional = **229 total**

## Load Tests

Load tests are written with [k6](https://grafana.com/docs/k6/). They require the full Docker Compose stack running.

```bash
# Start the stack
docker compose up -d

# Seed test data (tenant + booking types + availability windows)
docker exec -i chronith-postgres-1 psql -U chronith -d chronith <<'SQL'
  SET search_path = chronith;
  INSERT INTO tenants ("Id","Name","Slug","TimeZoneId","IsDeleted","CreatedAt")
  VALUES ('00000000-0000-0000-0000-000000000001','Test Tenant','test-tenant','UTC',false,NOW())
  ON CONFLICT ("Id") DO NOTHING;
SQL

# Run a script
k6 run tests/Chronith.Tests.Load/scripts/availability.js \
  --env BASE_URL=http://localhost:5001 \
  --env JWT_SIGNING_KEY=change-me-in-production-at-least-32-chars
```

Available scripts: `availability.js`, `create-booking.js`, `booking-lifecycle.js`, `concurrent-booking.js`.

See [`tests/Chronith.Tests.Load/README.md`](tests/Chronith.Tests.Load/README.md) for details on each scenario.

## Benchmarks

Micro-benchmarks use [BenchmarkDotNet](https://benchmarkdotnet.org/). They measure hot-path logic (mapping, serialization, domain operations) without a running database.

```bash
dotnet run -c Release --project tests/Chronith.Tests.Performance -- --filter "*"
```

Results are written to `BenchmarkDotNet.Artifacts/`.

## CI/CD

GitHub Actions workflow (`.github/workflows/ci.yml`) runs on every push to `main`/`develop` and on pull requests targeting those branches.

| Job                   | Trigger                  | Description                                                     |
| --------------------- | ------------------------ | --------------------------------------------------------------- |
| `.NET Tests`          | all                      | Build, then run all 229 tests with a Postgres service container |
| `Docker Build`        | all                      | Build the production Docker image (not pushed)                  |
| `k6 Load Tests`       | all                      | Run all four k6 scripts against a local Compose stack           |
| `Benchmarks`          | push to `main`/`develop` | Run BenchmarkDotNet; upload results as artifacts                |
| `CodeQL (csharp)`     | push to `main`, PRs      | Static analysis — C#                                            |
| `CodeQL (javascript)` | push to `main`, PRs      | Static analysis — JavaScript                                    |

## Project Structure

```
chronith/
├── src/
│   ├── Chronith.API/               ← HTTP layer (FastEndpoints, middleware, health checks)
│   ├── Chronith.Application/       ← CQRS (MediatR commands/queries), DTOs, validators
│   ├── Chronith.Domain/            ← Entities, domain events, state machine, value objects
│   └── Chronith.Infrastructure/    ← EF Core, Npgsql, migrations, repositories
├── tests/
│   ├── Chronith.Tests.Unit/        ← Pure unit tests (no I/O)
│   ├── Chronith.Tests.Integration/ ← Repository and DB tests (Testcontainers)
│   ├── Chronith.Tests.Functional/  ← End-to-end HTTP tests (WebApplicationFactory)
│   ├── Chronith.Tests.Performance/ ← BenchmarkDotNet micro-benchmarks
│   └── Chronith.Tests.Load/        ← k6 load test scripts
├── docs/plans/                     ← Architecture decision records and design docs
├── .github/workflows/ci.yml        ← GitHub Actions CI pipeline
├── docker-compose.yml              ← API + PostgreSQL
├── docker-compose.override.yml     ← Local dev overrides
├── Chronith.slnx                   ← Solution file
└── global.json                     ← SDK version pin (10.0.100)
```
