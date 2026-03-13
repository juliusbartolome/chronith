---
title: Recurring Bookings
description: Create and manage recurring booking series in Chronith.
---

Recurring bookings allow customers to book a repeating series of appointments using standard RRULE syntax.

## Create a recurring series

```sh
POST /v1/bookings/recurring
Authorization: Bearer <token>
```

```json
{
  "bookingTypeId": "3fa85f64-...",
  "startTime": "2026-04-15T09:00:00Z",
  "customerName": "Jane Doe",
  "customerEmail": "jane@example.com",
  "rrule": "FREQ=WEEKLY;COUNT=8;BYDAY=MO,WE,FR"
}
```

The `rrule` field accepts any valid [RFC 5545](https://datatracker.ietf.org/doc/html/rfc5545) RRULE string.

## Common RRULE patterns

| Pattern | RRULE |
|---------|-------|
| Weekly on Monday | `FREQ=WEEKLY;BYDAY=MO` |
| Daily for 5 days | `FREQ=DAILY;COUNT=5` |
| Monthly on the 1st | `FREQ=MONTHLY;BYMONTHDAY=1` |
| Weekdays for 4 weeks | `FREQ=WEEKLY;COUNT=20;BYDAY=MO,TU,WE,TH,FR` |

## List occurrences

```sh
GET /v1/bookings/series/{seriesId}
Authorization: Bearer <token>
```

Returns all individual booking occurrences in the series.

## Cancel an entire series

```sh
DELETE /v1/bookings/series/{seriesId}
Authorization: Bearer <token>
```

Cancels all future occurrences in the series. Past occurrences are not affected.

## Cancel a single occurrence

```sh
POST /v1/bookings/{id}/cancel
Authorization: Bearer <token>
```

Cancel individual occurrences without affecting the rest of the series.
