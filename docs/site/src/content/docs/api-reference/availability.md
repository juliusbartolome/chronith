---
title: Availability API
description: REST endpoints for querying availability in Chronith.
---

## Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET` | `/v1/booking-types/{id}/availability` | Get available slots | Admin/Staff |
| `GET` | `/v1/public/{tenantSlug}/booking-types/{id}/availability` | Get available slots (public) | None |

## Query parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `from` | `date` | Start of date range (ISO 8601 date, e.g. `2026-04-15`) |
| `to` | `date` | End of date range (ISO 8601 date) |
| `staffMemberId` | `uuid` | Filter by specific staff member (optional) |

## Response

```json
{
  "bookingTypeId": "3fa85f64-...",
  "slots": [
    {
      "startTime": "2026-04-15T09:00:00Z",
      "endTime": "2026-04-15T10:00:00Z",
      "available": true,
      "remainingCapacity": 1
    },
    {
      "startTime": "2026-04-15T10:00:00Z",
      "endTime": "2026-04-15T11:00:00Z",
      "available": false,
      "remainingCapacity": 0
    }
  ]
}
```

A slot is available if:
- It falls within a configured availability window
- No conflicting booking exists (or `remainingCapacity > 0`)
- At least one staff member is free (if staff is configured)
