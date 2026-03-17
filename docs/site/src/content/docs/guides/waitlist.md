---
title: Waitlist
description: Manage the waitlist for fully booked time slots in Chronith.
---

When a time slot is fully booked, customers can join a waitlist. If a booking is cancelled, waitlisted customers are automatically notified.

## Join the waitlist

```sh
POST /v1/bookings/{bookingTypeId}/waitlist
Authorization: Bearer <token>
```

```json
{
  "startTime": "2026-04-15T09:00:00Z",
  "customerName": "Jane Doe",
  "customerEmail": "jane@example.com"
}
```

## Offer expiry

When a slot opens up, the first customer on the waitlist receives an offer notification. The offer expires after **24 hours** by default.

If the customer does not accept within the expiry window, the offer moves to the next person on the waitlist.

## Dashboard management

Tenant admins can view and manage the waitlist from the dashboard:

```sh
GET /v1/booking-types/{id}/waitlist
Authorization: Bearer <token>
```

## Remove from waitlist

```sh
DELETE /v1/waitlist/{waitlistEntryId}
Authorization: Bearer <token>
```

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `Waitlist__OfferExpiryHours` | `24` | Hours before a waitlist offer expires |
