---
title: Customer Accounts
description: Register, login, and manage customer accounts in Chronith.
---

Customers can create accounts to manage their bookings across a tenant.

## Register

```sh
POST /v1/customers/register
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
POST /v1/customers/login
```

```json
{
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
POST /v1/customers/refresh
```

```json
{ "refreshToken": "def50200..." }
```

## OIDC integration

Chronith supports OpenID Connect (OIDC) for customer authentication. Configure an OIDC provider in tenant settings to allow customers to sign in with their existing identity provider (Google, Microsoft, etc.).

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
