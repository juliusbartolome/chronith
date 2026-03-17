---
title: Staff API
description: REST endpoints for managing staff members in Chronith.
---

## Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET` | `/v1/staff` | List staff members | Admin |
| `POST` | `/v1/staff` | Create a staff member | Admin |
| `GET` | `/v1/staff/{id}` | Get a staff member | Admin/Staff |
| `PUT` | `/v1/staff/{id}` | Update a staff member | Admin |
| `DELETE` | `/v1/staff/{id}` | Deactivate a staff member | Admin |
| `GET` | `/v1/staff/{id}/availability` | Get staff availability windows | Admin/Staff |

## Request body: Create staff member

```json
{
  "name": "string",
  "email": "string",
  "role": "Staff | Admin"
}
```

## Response: Staff member

```json
{
  "id": "3fa85f64-...",
  "tenantId": "3fa85f64-...",
  "name": "Alice Johnson",
  "email": "alice@mybusiness.com",
  "role": "Staff",
  "isActive": true,
  "availabilityWindows": [
    {
      "id": "3fa85f64-...",
      "dayOfWeek": "Monday",
      "startTime": "09:00",
      "endTime": "17:00"
    }
  ]
}
```
