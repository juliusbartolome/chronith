---
title: Bookings API
description: REST endpoints for managing bookings in Chronith.
---

## Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET` | `/v1/bookings` | List bookings | Admin/Staff |
| `POST` | `/v1/bookings` | Create a booking | Admin/Staff/Customer |
| `GET` | `/v1/bookings/{id}` | Get a booking | Admin/Staff/Customer |
| `DELETE` | `/v1/bookings/{id}` | Cancel a booking | Admin/Staff |
| `POST` | `/v1/bookings/{id}/confirm` | Confirm a booking | Admin/Staff |
| `POST` | `/v1/bookings/{id}/cancel` | Cancel a booking | Admin/Staff/Customer |
| `POST` | `/v1/bookings/{id}/reschedule` | Reschedule a booking | Admin/Staff |
| `POST` | `/v1/bookings/recurring` | Create a recurring series | Admin/Staff |
| `GET` | `/v1/bookings/series/{seriesId}` | List series occurrences | Admin/Staff |
| `DELETE` | `/v1/bookings/series/{seriesId}` | Cancel entire series | Admin/Staff |
| `POST` | `/v1/bookings/{id}/payment/initiate` | Initiate payment | Customer |
| `POST` | `/v1/bookings/{id}/refund` | Issue refund | Admin |

## Request body: Create booking

```json
{
  "bookingTypeId": "string (uuid)",
  "startTime": "string (ISO 8601)",
  "customerName": "string",
  "customerEmail": "string",
  "staffMemberId": "string (uuid, optional)",
  "customFields": { "fieldName": "value" }
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
  "bookingTypeId": "string (uuid)",
  "startTime": "string (ISO 8601)",
  "customerName": "string",
  "customerEmail": "string",
  "rrule": "string (RRULE)"
}
```
