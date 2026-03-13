---
title: Staff Management
description: Create and manage staff members in Chronith.
---

Staff members are linked to a tenant and can be assigned to bookings.

## Create a staff member

```sh
POST /v1/staff
Authorization: Bearer <token>
```

```json
{
  "name": "Alice Johnson",
  "email": "alice@mybusiness.com",
  "role": "Staff"
}
```

## Assign availability windows

Define when a staff member is available for bookings:

```sh
POST /v1/staff/{id}/availability-windows
Authorization: Bearer <token>
```

```json
{
  "dayOfWeek": "Tuesday",
  "startTime": "10:00",
  "endTime": "18:00"
}
```

Multiple windows can be added for different days.

## List staff members

```sh
GET /v1/staff
Authorization: Bearer <token>
```

## Get a staff member

```sh
GET /v1/staff/{id}
Authorization: Bearer <token>
```

## Deactivate a staff member

```sh
DELETE /v1/staff/{id}
Authorization: Bearer <token>
```

Deactivated staff members are soft-deleted and can no longer be assigned to new bookings.
