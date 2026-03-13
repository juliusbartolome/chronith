---
title: Bookings API
description: REST endpoints for managing bookings in Chronith.
---

## Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET` | `/v1/booking-types/{slug}/bookings` | List bookings for a booking type (paginated) | Admin/Staff |
| `POST` | `/v1/booking-types/{slug}/bookings` | Create a new booking (`slug` is the booking type slug) | Admin/Staff/Customer |
| `GET` | `/v1/bookings/{id}` | Get a booking by ID | Admin/Staff/Customer |
| `DELETE` | `/v1/bookings/{id}` | Soft-delete a booking | Admin/Staff |
| `POST` | `/v1/bookings/{id}/confirm` | Confirm a booking | Admin/Staff |
| `POST` | `/v1/bookings/{id}/cancel` | Cancel a booking | Admin/Staff/Customer |
| `POST` | `/v1/bookings/{id}/reschedule` | Reschedule a booking | Admin/Staff |
| `GET` | `/v1/bookings/series/{seriesId}` | List series occurrences | Admin/Staff |
| `POST` | `/v1/bookings/series/{seriesId}/cancel` | Cancel all future occurrences | Admin/Staff |
| `POST` | `/v1/bookings/recurring` | Create a recurring booking series | Admin/Staff |

## Request body: Create booking

```json
{
  "customerEmail": "string"
}
```

## Request body: Reschedule

```json
{ "newStartTime": "string (ISO 8601)" }
```

## Request body: Cancel

```json
{ "reason": "string (optional)" }
```

## Request body: Create recurring

```json
{
  "customerEmail": "string",
  "rrule": "string (RRULE)"
}
```
