---
title: Booking Types API
description: REST endpoints for managing booking types in Chronith.
---

## Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET` | `/v1/booking-types` | List booking types | Admin/Staff |
| `POST` | `/v1/booking-types` | Create a booking type | Admin |
| `GET` | `/v1/booking-types/{id}` | Get a booking type | Admin/Staff |
| `PUT` | `/v1/booking-types/{id}` | Update a booking type | Admin |
| `DELETE` | `/v1/booking-types/{id}` | Deactivate a booking type | Admin |
| `POST` | `/v1/booking-types/{id}/availability-windows` | Add availability window | Admin |
| `DELETE` | `/v1/booking-types/{id}/availability-windows/{windowId}` | Remove availability window | Admin |
| `POST` | `/v1/booking-types/{id}/custom-fields` | Add custom field | Admin |
| `DELETE` | `/v1/booking-types/{id}/custom-fields/{fieldId}` | Remove custom field | Admin |

## Request body: Create booking type

```json
{
  "title": "string",
  "kind": "TimeSlot | Calendar",
  "durationMinutes": "integer (required for TimeSlot)",
  "priceCentavos": "integer (0 = free)",
  "description": "string (optional)",
  "maxBookingsPerSlot": "integer (optional)"
}
```

## Request body: Availability window

```json
{
  "dayOfWeek": "Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday",
  "startTime": "string (HH:mm)",
  "endTime": "string (HH:mm)"
}
```

## Request body: Custom field

```json
{
  "name": "string (identifier)",
  "label": "string (display label)",
  "type": "Text | TextArea | Number | Select | MultiSelect | Checkbox | Date",
  "required": "boolean",
  "options": ["string"] 
}
```
