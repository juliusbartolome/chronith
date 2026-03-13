---
title: Tenant API
description: REST endpoints for tenant settings and subscription management in Chronith.
---

## Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET` | `/v1/settings` | Get tenant settings | Admin |
| `PATCH` | `/v1/settings` | Update tenant settings | Admin |
| `POST` | `/v1/settings/branding` | Update branding | Admin |
| `POST` | `/v1/settings/public-booking` | Configure public booking | Admin |
| `GET` | `/v1/tenants/subscription` | Get current plan | Admin |
| `POST` | `/v1/tenants/subscription` | Upgrade plan | Admin |

## Request body: Tenant settings

```json
{
  "timezone": "string (IANA timezone, e.g. Asia/Manila)",
  "currency": "PHP",
  "defaultBookingDurationMinutes": "integer",
  "requirePaymentUpfront": "boolean"
}
```

## Request body: Branding

```json
{
  "logoUrl": "string (URL)",
  "primaryColor": "string (hex color)",
  "businessName": "string"
}
```

## Request body: Public booking settings

```json
{
  "enabled": "boolean",
  "allowGuestBookings": "boolean"
}
```

## Request body: Subscription upgrade

```json
{ "plan": "Free | Starter | Pro | Enterprise" }
```

## Response: Subscription

```json
{
  "plan": "Pro",
  "status": "Active",
  "currentPeriodStart": "2026-01-01T00:00:00Z",
  "currentPeriodEnd": "2026-02-01T00:00:00Z",
  "bookingsUsed": 1234,
  "bookingsLimit": 5000
}
```
