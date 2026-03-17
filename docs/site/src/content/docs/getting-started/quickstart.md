---
title: Quickstart
description: Get Chronith running in under 5 minutes using Docker Compose.
sidebar:
  order: 1
---

## Prerequisites

- Docker 24+
- Docker Compose v2+
- `curl` or an HTTP client

## 1. Clone the repository

```sh
git clone https://github.com/juliusbartolome/chronith.git
cd chronith
```

## 2. Start the stack

```sh
docker compose up -d
```

This starts:
- **chronith-api** on port `5001`
- **PostgreSQL 17** on port `5432`
- **Redis 8** on port `6379`

Wait ~10 seconds for the API to start and run migrations.

## 3. Health check

```sh
curl http://localhost:5001/health
```

Expected:

```json
{ "status": "Healthy" }
```

## 4. Create a tenant

```sh
curl -X POST http://localhost:5001/v1/signup \
  -H "Content-Type: application/json" \
  -d '{
    "tenantName": "My Business",
    "tenantSlug": "my-business",
    "timeZoneId": "Asia/Manila",
    "email": "admin@mybusiness.com",
    "password": "Admin1234!"
  }'
```

Save the `tenantId` and `token` from the response.

## 5. Create a booking type

```sh
curl -X POST http://localhost:5001/v1/booking-types \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Consultation",
    "kind": "TimeSlot",
    "durationMinutes": 60,
    "priceCentavos": 150000
  }'
```

## 6. Open the dashboard

Navigate to `http://localhost:3001` — the dashboard starts automatically with `docker compose up -d`.

## Next steps

- [Configuration reference](/getting-started/configuration)
- [Booking Types guide](/guides/booking-types)
- [API Reference](/api-reference/overview)
