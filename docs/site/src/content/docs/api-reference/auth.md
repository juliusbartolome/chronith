---
title: Auth API
description: REST endpoints for authentication in Chronith.
---

## Admin and Staff auth

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/auth/admin/login` | Admin login |
| `POST` | `/v1/auth/staff/login` | Staff login |
| `POST` | `/v1/auth/refresh` | Refresh JWT token |
| `POST` | `/v1/auth/logout` | Logout (invalidate refresh token) |

### Request body: Login

```json
{
  "email": "string",
  "password": "string"
}
```

### Response: Login

```json
{
  "token": "string (JWT)",
  "refreshToken": "string",
  "expiresAt": "string (ISO 8601)",
  "userId": "string (uuid)",
  "role": "Admin | Staff"
}
```

## Customer auth

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/customers/register` | Register customer |
| `POST` | `/v1/customers/login` | Customer login |
| `POST` | `/v1/customers/refresh` | Refresh customer token |

## API Keys

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET` | `/v1/api-keys` | List API keys | Admin |
| `POST` | `/v1/api-keys` | Create API key | Admin |
| `DELETE` | `/v1/api-keys/{id}` | Revoke API key | Admin |

### Request body: Create API key

```json
{
  "name": "string (description for this key)",
  "expiresAt": "string (ISO 8601, optional)"
}
```

### Response: Create API key

```json
{
  "id": "3fa85f64-...",
  "name": "Production Key",
  "key": "chron_live_xxxxxxxxxxxxxxxx",
  "createdAt": "2026-01-01T00:00:00Z",
  "expiresAt": null
}
```

The `key` field is only returned once at creation time. Store it securely.
