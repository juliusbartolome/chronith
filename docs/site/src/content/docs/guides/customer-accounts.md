---
title: Customer Accounts
description: Register, login, and manage customer accounts in Chronith.
---

Customers can create accounts to manage their bookings across a tenant.

## Register

```sh
POST /v1/auth/register
```

```json
{
  "tenantId": "3fa85f64-...",
  "name": "Jane Doe",
  "email": "jane@example.com",
  "password": "SecurePassword123!"
}
```

## Login

```sh
POST /v1/auth/login
```

```json
{
  "tenantSlug": "my-business",
  "email": "jane@example.com",
  "password": "SecurePassword123!"
}
```

Response:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "def50200...",
  "expiresAt": "2026-04-15T10:00:00Z"
}
```

## Refresh token

```sh
POST /v1/auth/refresh
```

```json
{ "refreshToken": "def50200..." }
```

## Customer profile

```sh
GET /v1/customers/me
Authorization: Bearer <token>
```

## Customer bookings

```sh
GET /v1/customers/me/bookings
Authorization: Bearer <token>
```

Returns all bookings for the authenticated customer.
