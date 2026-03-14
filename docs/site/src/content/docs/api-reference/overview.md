---
title: API Reference Overview
description: Chronith REST API overview, authentication, error handling, and conventions.
sidebar:
  order: 1
---

## Base URL

```
http://localhost:5001
```

In production, replace with your deployed API URL. All endpoints are versioned under `/v1/`.

## Authentication

### JWT Bearer

```
Authorization: Bearer <token>
```

Obtain a token via:
- `POST /v1/auth/admin/login`
- `POST /v1/auth/staff/login`
- `POST /v1/customers/login`

### API Key

```
X-Api-Key: <api-key>
```

Create API keys via `POST /v1/api-keys`.

## Error responses (RFC 7807)

All errors follow [RFC 7807 Problem Details](https://datatracker.ietf.org/doc/html/rfc7807):

```json
{
  "type": "https://chronith.io/errors/not-found",
  "title": "Not Found",
  "status": 404,
  "detail": "Booking with ID '3fa85f64' was not found.",
  "instance": "/v1/bookings/3fa85f64"
}
```

## Pagination

List endpoints return a paginated envelope:

```json
{
  "items": [...],
  "totalCount": 100,
  "page": 1,
  "pageSize": 20,
  "totalPages": 5
}
```

Query params: `?page=1&pageSize=20`

## Rate limiting

Rate limiting headers are returned on every response:

| Header | Description |
|--------|-------------|
| `X-RateLimit-Limit` | Maximum requests per window |
| `X-RateLimit-Remaining` | Requests remaining in current window |
| `X-RateLimit-Reset` | Unix timestamp when the window resets |

When exceeded: `429 Too Many Requests`.

## Versioning

The current API version is `v1`. All endpoints are prefixed `/v1/`. Breaking changes will be introduced in new versions (`v2/`, etc.) with a deprecation notice.
