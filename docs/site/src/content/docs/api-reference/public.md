---
title: Public API
description: Public REST endpoints requiring no authentication in Chronith.
---

These endpoints are accessible without authentication and are designed for use in public booking pages and embedded widgets.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/v1/public/{tenantSlug}` | Get tenant public profile |
| `GET` | `/v1/public/{tenantSlug}/booking-types` | List public booking types |
| `GET` | `/v1/public/{tenantSlug}/booking-types/{id}` | Get booking type details |
| `GET` | `/v1/public/{tenantSlug}/booking-types/{id}/availability` | Get available slots |
| `POST` | `/v1/public/{tenantSlug}/bookings` | Create a public booking |
| `GET` | `/v1/public/{tenantSlug}/bookings/{id}` | Get booking status |
| `GET` | `/v1/public/{tenantSlug}/ical` | Download iCal feed |

## Create a public booking

```sh
POST /v1/public/{tenantSlug}/bookings
```

```json
{
  "bookingTypeId": "3fa85f64-...",
  "startTime": "2026-04-15T09:00:00Z",
  "customerName": "Jane Doe",
  "customerEmail": "jane@example.com",
  "customFields": {}
}
```

No authentication required. Requires the tenant's public booking page to be enabled.

## iCal feed

Download an iCal (`.ics`) file with all confirmed bookings for a tenant:

```
GET /v1/public/{tenantSlug}/ical
```

This feed can be subscribed to in any calendar application (Apple Calendar, Google Calendar, Outlook).

## Rate limiting

Public endpoints have a more restrictive rate limit to prevent abuse. See the [API Reference Overview](/api-reference/overview) for rate limit headers.
