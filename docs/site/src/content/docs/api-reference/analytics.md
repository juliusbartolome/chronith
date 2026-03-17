---
title: Analytics API
description: REST endpoints for analytics and reporting in Chronith.
---

## Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET` | `/v1/analytics/bookings` | Bookings summary | Admin |
| `GET` | `/v1/analytics/revenue` | Revenue summary | Admin |
| `GET` | `/v1/analytics/utilization` | Staff utilization | Admin |
| `GET` | `/v1/analytics/bookings/export` | Export bookings | Admin |

## Query parameters

All analytics endpoints accept:

| Parameter | Type | Description |
|-----------|------|-------------|
| `from` | `date` | Start date (ISO 8601, e.g. `2026-01-01`) |
| `to` | `date` | End date (ISO 8601) |
| `groupBy` | `string` | `day`, `week`, or `month` |

The export endpoint additionally accepts:

| Parameter | Type | Description |
|-----------|------|-------------|
| `format` | `string` | `csv` or `pdf` |

## Response: Bookings summary

```json
{
  "totalBookings": 248,
  "confirmedBookings": 200,
  "cancelledBookings": 48,
  "data": [
    { "period": "2026-01", "count": 80, "confirmed": 70, "cancelled": 10 },
    { "period": "2026-02", "count": 90, "confirmed": 75, "cancelled": 15 }
  ]
}
```

## Response: Revenue summary

```json
{
  "totalRevenueCentavos": 120000000,
  "data": [
    { "period": "2026-01", "revenueCentavos": 40000000 },
    { "period": "2026-02", "revenueCentavos": 45000000 }
  ]
}
```
