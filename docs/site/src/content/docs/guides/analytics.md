---
title: Analytics
description: Query booking analytics and export reports in Chronith.
---

Chronith provides booking analytics endpoints for revenue, utilization, and booking counts.

## Bookings analytics

```sh
GET /v1/analytics/bookings?from=2026-01-01&to=2026-04-01&groupBy=month
Authorization: Bearer <token>
```

## Revenue analytics

```sh
GET /v1/analytics/revenue?from=2026-01-01&to=2026-04-01&groupBy=week
Authorization: Bearer <token>
```

## Utilization analytics

```sh
GET /v1/analytics/utilization?from=2026-01-01&to=2026-04-01&groupBy=day
Authorization: Bearer <token>
```

## groupBy options

| Value | Description |
|-------|-------------|
| `day` | Group by calendar day |
| `week` | Group by ISO week |
| `month` | Group by calendar month |

## Export

### CSV export

```sh
GET /v1/analytics/bookings/export?format=csv&from=2026-01-01&to=2026-04-01
Authorization: Bearer <token>
```

### PDF export

```sh
GET /v1/analytics/bookings/export?format=pdf&from=2026-01-01&to=2026-04-01
Authorization: Bearer <token>
```

Exports can also be triggered from the dashboard.

## Caching

Analytics results are cached in **Redis** for 5 minutes by default to reduce database load. Cache is invalidated when new bookings are created or cancelled.
