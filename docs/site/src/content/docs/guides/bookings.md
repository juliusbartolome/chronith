---
title: Bookings
description: Manage the full booking lifecycle in Chronith.
---

## Booking status state machine

```
PendingPayment → PendingVerification → Confirmed
      ↓                  ↓                ↓
   Cancelled          Cancelled        Cancelled
```

**Free bookings** (price = 0) skip `PendingPayment` and go directly to `PendingVerification`.

| Status | Description |
|--------|-------------|
| `PendingPayment` | Awaiting payment from the customer |
| `PendingVerification` | Payment received, awaiting staff confirmation |
| `Confirmed` | Booking is confirmed |
| `Cancelled` | Booking has been cancelled |

## Create a booking

```sh
POST /v1/bookings
Authorization: Bearer <token>
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

## Confirm a booking

Move a booking from `PendingVerification` to `Confirmed`:

```sh
POST /v1/bookings/{id}/confirm
Authorization: Bearer <token>
```

## Cancel a booking

```sh
POST /v1/bookings/{id}/cancel
Authorization: Bearer <token>
```

```json
{ "reason": "Customer requested cancellation" }
```

## Reschedule a booking

```sh
POST /v1/bookings/{id}/reschedule
Authorization: Bearer <token>
```

```json
{ "newStartTime": "2026-04-16T10:00:00Z" }
```

## Waitlist

If a slot is full, customers can join the waitlist. See the [Waitlist guide](/guides/waitlist).

## Recurring bookings

Create a series of recurring bookings using RRULE. See the [Recurring Bookings guide](/guides/recurring-bookings).
