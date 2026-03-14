---
title: Deployment
description: Deployment options for Chronith including Docker Compose and Kubernetes.
---

## Docker Compose (recommended for development)

```sh
# Start full stack
docker compose up -d

# Stop stack
docker compose down

# View logs
docker compose logs -f chronith-api
```

### Services

| Service | Image | Port |
|---------|-------|------|
| `chronith-api` | Multi-stage .NET build | 5001 |
| `postgres` | `postgres:17-alpine` | 5432 |
| `redis` | `redis:8-alpine` | 6379 |

## Dockerfile

Chronith uses a multi-stage Dockerfile:

1. **build** — `mcr.microsoft.com/dotnet/sdk:10.0` — restores and builds in Release mode
2. **publish** — publishes the app
3. **final** — `mcr.microsoft.com/dotnet/aspnet:10.0` — minimal runtime image

Build:

```sh
docker build -t chronith-api .
```

## Required environment variables

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Redis__ConnectionString` | Redis connection string |
| `Jwt__SigningKey` | JWT HMAC key (min 32 chars) |
| `Security__EncryptionKey` | AES-256-GCM key (Base64, 32 bytes) |

Generate keys:

```sh
openssl rand -base64 32  # Use for Jwt__SigningKey and Security__EncryptionKey
```

## Kubernetes

A minimal Kubernetes deployment outline:

- **Deployment:** `chronith-api` with health checks on `/health`
- **Service:** `ClusterIP` on port `8080`, expose via Ingress on `5001`
- **ConfigMap:** Non-secret configuration
- **Secret:** `Jwt__SigningKey`, `Security__EncryptionKey`, connection strings
- **HorizontalPodAutoscaler:** Scale on CPU usage

PostgreSQL and Redis should be managed services (e.g., Azure Database for PostgreSQL, Azure Cache for Redis) in production.

## Health checks

```sh
GET /health         # Overall health
GET /health/ready   # Readiness (DB + Redis)
GET /health/live    # Liveness
```
