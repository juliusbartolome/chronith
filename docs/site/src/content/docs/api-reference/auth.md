---
title: Authentication
description: JWT and API key authentication endpoints.
---

## Tenant admin / staff authentication

| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1/auth/login` | Login → JWT + refresh token |
| POST | `/v1/auth/refresh` | Refresh access token |
| GET | `/v1/auth/me` | Get current user info |

### Login request body

```json
{
  "tenantSlug": "my-business",
  "email": "admin@mybusiness.com",
  "password": "Admin1234!"
}
```

## Customer authentication

| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1/auth/register` | Register a new user account |
| POST | `/v1/auth/login` | Customer login (same endpoint) |
| POST | `/v1/auth/refresh` | Refresh customer token |
| GET | `/v1/auth/me` | Get current customer info |

## API keys

API keys are passed via the `X-Api-Key` header. Management of API keys is handled through the tenant dashboard.
