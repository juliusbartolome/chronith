---
title: Data Export
description: Export bookings and analytics data from Chronith.
---

Chronith supports exporting your data in CSV and PDF formats.

## Export bookings to CSV

```sh
GET /v1/analytics/bookings/export?format=csv&from=2026-01-01&to=2026-04-01
Authorization: Bearer <token>
```

The response is a `text/csv` file attachment.

### CSV columns

| Column | Description |
|--------|-------------|
| `id` | Booking UUID |
| `bookingTypeTitle` | Booking type name |
| `customerName` | Customer full name |
| `customerEmail` | Customer email |
| `startTime` | Booking start (ISO 8601) |
| `endTime` | Booking end (ISO 8601) |
| `status` | Booking status |
| `priceCentavos` | Price in centavos |
| `staffName` | Assigned staff member |
| `createdAt` | Record creation time |

## Export analytics to PDF

```sh
GET /v1/analytics/bookings/export?format=pdf&from=2026-01-01&to=2026-04-01
Authorization: Bearer <token>
```

The PDF report includes:
- Bookings summary chart
- Revenue breakdown
- Staff utilization

## Dashboard export

Export buttons are available in the dashboard under **Analytics > Export**.

Select:
- Date range
- Format (CSV or PDF)
- Report type (Bookings, Revenue, or Utilization)

Click **Download** to trigger the export.
