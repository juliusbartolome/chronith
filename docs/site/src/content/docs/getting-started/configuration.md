---
title: Configuration
description: Full environment variable and appsettings reference for Chronith.
sidebar:
  order: 3
---

All configuration can be set as environment variables or in `appsettings.json`.
Environment variables override file values.

## Database

| Variable | Default | Description |
|----------|---------|-------------|
| `ConnectionStrings__DefaultConnection` | — | PostgreSQL connection string. **Required.** |

Example:

```
Host=localhost;Port=5432;Database=chronith;Username=postgres;Password=postgres
```

## Redis

| Variable | Default | Description |
|----------|---------|-------------|
| `Redis__ConnectionString` | `localhost:6379` | Redis connection string |

## JWT

| Variable | Default | Description |
|----------|---------|-------------|
| `Jwt__SigningKey` | — | HMAC-SHA256 signing key (min 32 chars). **Required.** |
| `Jwt__Issuer` | `Chronith` | JWT issuer claim |
| `Jwt__Audience` | `Chronith` | JWT audience claim |
| `Jwt__ExpiryMinutes` | `60` | Token lifetime in minutes |

Generate a secure key:

```sh
openssl rand -base64 32
```

## Security

| Variable | Default | Description |
|----------|---------|-------------|
| `Security__EncryptionKey` | — | Base64-encoded 32-byte AES-256-GCM key. **Required for notifications.** |

Generate:

```sh
openssl rand -base64 32
```

## Observability

| Variable | Default | Description |
|----------|---------|-------------|
| `Otel__Endpoint` | — | OTLP exporter endpoint (e.g. `http://otel-collector:4317`) |
| `Otel__ServiceName` | `chronith-api` | Service name in traces/metrics |

## Payments

| Variable | Default | Description |
|----------|---------|-------------|
| `Payments__Provider` | `Stub` | Payment provider: `Stub` or `PayMongo` |
| `Payments__PayMongo__SecretKey` | — | PayMongo secret key |
| `Payments__PayMongo__PublicKey` | — | PayMongo public key |

## Rate Limiting

| Variable | Default | Description |
|----------|---------|-------------|
| `RateLimiting__PermitLimit` | `100` | Requests per window |
| `RateLimiting__WindowSeconds` | `60` | Window size in seconds |
