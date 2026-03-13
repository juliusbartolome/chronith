---
title: Booking Types
description: Create and manage booking types in Chronith.
---

Booking types define the service or appointment that customers can book. Chronith supports two kinds.

## Kinds

| Kind | Description |
|------|-------------|
| `TimeSlot` | Appointments of fixed duration at specific times |
| `Calendar` | Full-day or multi-day reservations |

## Create a booking type

```sh
POST /v1/booking-types
Authorization: Bearer <token>
```

```json
{
  "title": "60-min Consultation",
  "kind": "TimeSlot",
  "durationMinutes": 60,
  "priceCentavos": 150000,
  "description": "One-on-one consultation session"
}
```

A `CalendarBookingType` omits `durationMinutes`.

## Availability windows

Availability windows define when a booking type is bookable.

```sh
POST /v1/booking-types/{id}/availability-windows
```

```json
{
  "dayOfWeek": "Monday",
  "startTime": "09:00",
  "endTime": "17:00"
}
```

## Custom fields

Add custom fields to collect additional information at booking time. See the [Custom Fields guide](/guides/custom-fields).

## Deactivating a booking type

```sh
DELETE /v1/booking-types/{id}
```

Deactivated booking types are soft-deleted and no longer appear in the public booking page.
