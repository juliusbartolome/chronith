---
title: Audit Logging
description: Query and manage audit logs in Chronith.
---

Chronith logs all significant actions to an audit log for compliance and debugging.

## Query audit log

```sh
GET /v1/audit-log?from=2026-01-01&to=2026-04-01&page=1&pageSize=50
Authorization: Bearer <token>
```

Filter by actor:

```sh
GET /v1/audit-log?actorId=3fa85f64-...
```

## Audit entry structure

```json
{
  "id": "3fa85f64-...",
  "tenantId": "3fa85f64-...",
  "actorId": "3fa85f64-...",
  "actorEmail": "admin@mybusiness.com",
  "action": "booking.confirmed",
  "resourceType": "Booking",
  "resourceId": "3fa85f64-...",
  "metadata": { "bookingId": "..." },
  "occurredAt": "2026-04-15T10:00:00Z",
  "ipAddress": "192.168.1.1"
}
```

## Retention by plan

| Plan | Retention |
|------|-----------|
| Free | 7 days |
| Starter | 30 days |
| Pro | 1 year |
| Enterprise | Unlimited |

Entries older than the retention period are automatically purged by a background service.

## Audit events

| Action | Description |
|--------|-------------|
| `booking.created` | Booking was created |
| `booking.confirmed` | Booking was confirmed |
| `booking.cancelled` | Booking was cancelled |
| `booking.rescheduled` | Booking was rescheduled |
| `payment.received` | Payment was received |
| `staff.created` | Staff member was created |
| `staff.deactivated` | Staff member was deactivated |
| `settings.updated` | Tenant settings were updated |
| `tenant.plan_upgraded` | Plan was upgraded |
