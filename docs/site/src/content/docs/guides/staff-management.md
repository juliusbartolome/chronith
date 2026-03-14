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

## Availability windows

Availability windows define when a staff member is available for bookings. Configure availability windows through the tenant dashboard (`/v1/staff/{id}/availability` for read access).

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
