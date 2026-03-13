---
title: Tenant Onboarding
description: Sign up, manage plans, and configure your tenant in Chronith.
---

## Sign up

Create a new tenant account:

```sh
POST /v1/tenants/signup
```

```json
{
  "businessName": "My Business",
  "slug": "my-business",
  "adminEmail": "admin@mybusiness.com",
  "adminPassword": "Admin1234!"
}
```

Response includes a JWT `token` for the admin user.

## Plans

| Plan | Monthly Price | Bookings/mo | Staff Members | Retention |
|------|--------------|-------------|---------------|-----------|
| Free | $0 | 50 | 1 | 7 days |
| Starter | $29 | 500 | 5 | 30 days |
| Pro | $79 | 5,000 | 25 | 1 year |
| Enterprise | Custom | Unlimited | Unlimited | Unlimited |

## Upgrade plan

```sh
POST /v1/tenants/subscription
Authorization: Bearer <token>
```

```json
{ "plan": "Pro" }
```

## Branding settings

Customize your public-facing pages:

```sh
POST /v1/settings/branding
Authorization: Bearer <token>
```

```json
{
  "logoUrl": "https://cdn.example.com/logo.png",
  "primaryColor": "#1a73e8",
  "businessName": "My Business",
  "timezone": "Asia/Manila"
}
```

## Tenant settings

```sh
GET /v1/settings
Authorization: Bearer <token>

PATCH /v1/settings
Authorization: Bearer <token>
```
