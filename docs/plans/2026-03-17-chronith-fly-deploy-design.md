# Chronith Fly.io Deployment Design — Minimum Set

**Date:** 2026-03-17
**Approach:** B — Handwritten `fly.toml` + `fly deploy`
**Branch:** `feat/fly-deploy` (from `develop`)

---

## Overview

Deploy the Chronith API to Fly.io using external managed services for Postgres (Neon) and Redis (Upstash). No observability stack, no PgBouncer, no dashboard — API only.

---

## Topology

```
Fly.io (sin — Singapore)
└── chronith-api  shared-cpu-1x · 256MB · auto-stop when idle

Neon (serverless Postgres, free tier)
└── Database__ConnectionString  →  Npgsql SSL connection string

Upstash (serverless Redis, free tier, TLS)
└── Redis__ConnectionString  →  StackExchange.Redis SSL format
```

**Excluded from this deployment:**

- PgBouncer (connect directly to Neon)
- Jaeger / Prometheus / Grafana (observability flags set to `false`)
- Dashboard (`dashboard` profile not activated)

---

## Why No Code Changes Are Needed

`Program.cs` already handles all three concerns that would otherwise require code:

| Concern            | How it's handled                                                                                                           |
| ------------------ | -------------------------------------------------------------------------------------------------------------------------- |
| EF Core migrations | `db.Database.MigrateAsync()` runs on startup (line 254) — idempotent                                                       |
| Observability      | Guarded by `if (observabilityOptions.EnableTracing)` and `if (observabilityOptions.EnableMetrics)` — disabled via env vars |
| Redis              | Guarded by `if (builder.Configuration.GetValue<bool>("Redis:Enabled"))` — enabled via env var                              |

---

## New File: `fly.toml`

One new file at the repo root.

**Key configuration:**

| Setting                   | Value           | Reason                                                                   |
| ------------------------- | --------------- | ------------------------------------------------------------------------ |
| `primary_region`          | `sin`           | Singapore — closest Fly region to Philippines                            |
| `internal_port`           | `8080`          | Matches `ASPNETCORE_URLS=http://+:8080`                                  |
| `auto_stop_machines`      | `stop`          | Scales to zero when idle (free tier friendly)                            |
| `min_machines_running`    | `0`             | Allow full scale-to-zero                                                 |
| Health check path         | `/health/live`  | Matches existing Dockerfile `HEALTHCHECK`                                |
| Health check grace period | `40s`           | Allows migrations + seeding to complete before Fly marks machine healthy |
| VM memory                 | `256mb`         | Sufficient for the .NET runtime at low load                              |
| VM CPU                    | `shared-cpu-1x` | Minimum for free/hobby tier                                              |

**Non-secret env vars in `[env]`:**

| Key                                 | Value          |
| ----------------------------------- | -------------- |
| `ASPNETCORE_ENVIRONMENT`            | `Production`   |
| `Database__Provider`                | `PostgreSQL`   |
| `Payments__Provider`                | `Stub`         |
| `Payments__Currency`                | `PHP`          |
| `Redis__Enabled`                    | `true`         |
| `Observability__EnableTracing`      | `false`        |
| `Observability__EnableMetrics`      | `false`        |
| `Observability__ServiceName`        | `chronith-api` |
| `Webhooks__DispatchIntervalSeconds` | `10`           |
| `Webhooks__HttpTimeoutSeconds`      | `10`           |
| `RateLimiting__Auth__PermitLimit`   | `100`          |
| `RateLimiting__Auth__WindowSeconds` | `300`          |

---

## Secrets

Set via `fly secrets set` — never committed to source control.

| Secret                       | Source                                                            |
| ---------------------------- | ----------------------------------------------------------------- |
| `Jwt__SigningKey`            | Minimum 32 chars — generate with `openssl rand -hex 32`           |
| `Security__EncryptionKey`    | Base64-encoded 32 bytes — generate with `openssl rand -base64 32` |
| `Database__ConnectionString` | Neon project → Connection Details → Npgsql format                 |
| `Redis__ConnectionString`    | Upstash → StackExchange.Redis format (see below)                  |

**Neon connection string format (Npgsql):**

```
Host=ep-xxx.region.aws.neon.tech;Database=neondb;Username=xxx;Password=xxx;Ssl Mode=Require;Trust Server Certificate=true
```

**Upstash connection string format (StackExchange.Redis):**

```
<host>:<port>,password=<password>,ssl=True,abortConnect=False
```

Upstash provides the URL as `rediss://:<password>@<host>:<port>` — convert to the above format for StackExchange.Redis.

---

## Deployment Steps

1. Create Neon project, create database, copy Npgsql connection string
2. Create Upstash database, copy Redis URL, convert to StackExchange.Redis format
3. `fly launch --no-deploy` — registers the app name on Fly.io, links to org, generates initial `fly.toml` stub (overwritten by our handwritten one)
4. `fly secrets set Jwt__SigningKey=... Security__EncryptionKey=... "Database__ConnectionString=..." "Redis__ConnectionString=..."`
5. `fly deploy` — Fly builds from `Dockerfile`, pushes image, starts machine; migrations and seeding run automatically on startup

---

## Success Criteria

- `fly deploy` exits 0
- `/health/live` returns HTTP 200
- `/health/ready` returns `{"status":"Healthy",...}` with `database`, `background-services`, and `redis` all healthy
- A test tenant can be created and a booking can be made via the API
