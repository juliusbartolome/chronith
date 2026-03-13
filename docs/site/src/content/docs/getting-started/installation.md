---
title: Installation
description: Self-hosted deployment options for Chronith.
sidebar:
  order: 2
---

## Docker Compose (recommended)

The fastest way to run Chronith locally or on a single server.

```sh
git clone https://github.com/juliusbartolome/chronith.git
cd chronith
docker compose up -d
```

Services started:

| Service | Port |
|---------|------|
| chronith-api | 5001 |
| PostgreSQL 17 | 5432 |
| Redis 8 | 6379 |

## Building from source

Requires: .NET 10 SDK, PostgreSQL 17, Redis 8.

```sh
dotnet build Chronith.slnx
dotnet ef database update \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API
dotnet run --project src/Chronith.API
```

## Environment variables

All configuration is done via environment variables or `appsettings.json`. See the [Configuration](/getting-started/configuration) page.

## Docker image

A pre-built Docker image is available:

```sh
docker pull ghcr.io/juliusbartolome/chronith:latest
```

Run it:

```sh
docker run -p 5001:8080 \
  -e ConnectionStrings__DefaultConnection="Host=db;Database=chronith;..." \
  -e Redis__ConnectionString="redis:6379" \
  -e Jwt__SigningKey="your-32-char-key" \
  ghcr.io/juliusbartolome/chronith:latest
```
