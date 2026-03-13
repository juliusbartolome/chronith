---
title: Availability
description: Query available time slots and manage conflict detection in Chronith.
---

## Query available slots

Get available time slots for a booking type within a date range:

```sh
GET /v1/booking-types/{id}/availability?from=2026-04-15&to=2026-04-22
Authorization: Bearer <token>
```

Response:

```json
{
  "slots": [
    { "startTime": "2026-04-15T09:00:00Z", "endTime": "2026-04-15T10:00:00Z", "available": true },
    { "startTime": "2026-04-15T10:00:00Z", "endTime": "2026-04-15T11:00:00Z", "available": false }
  ]
}
```

## Public availability endpoint

No authentication required for the public availability endpoint:

```sh
GET /v1/public/{tenantSlug}/booking-types/{id}/availability?from=2026-04-15&to=2026-04-22
```

## Conflict detection

When a booking is created, Chronith checks for slot conflicts. If a conflict exists, a `409 Conflict` response is returned:

```json
{
  "type": "https://chronith.io/errors/slot-conflict",
  "title": "Slot Conflict",
  "status": 409,
  "detail": "The requested time slot is already booked."
}
```

This is enforced by `SlotConflictException` at the domain level, ensuring no double-booking can occur.

## Staff availability

Availability also considers assigned staff member availability windows. A slot is only shown as available if at least one staff member is free during that time.
