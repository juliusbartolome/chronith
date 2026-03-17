---
title: Webhooks API
description: REST endpoints for managing webhooks in Chronith.
---

## Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET` | `/v1/webhooks` | List webhooks | Admin |
| `POST` | `/v1/webhooks` | Register a webhook | Admin |
| `GET` | `/v1/webhooks/{id}` | Get a webhook | Admin |
| `PUT` | `/v1/webhooks/{id}` | Update a webhook | Admin |
| `DELETE` | `/v1/webhooks/{id}` | Delete a webhook | Admin |
| `GET` | `/v1/webhooks/{id}/deliveries` | List delivery history | Admin |
| `POST` | `/v1/webhooks/{id}/deliveries/{deliveryId}/retry` | Retry a delivery | Admin |

## Request body: Register webhook

```json
{
  "url": "string (HTTPS URL)",
  "events": ["booking.created", "booking.confirmed"],
  "secret": "string (optional, for signature verification)"
}
```

## Response: Webhook

```json
{
  "id": "3fa85f64-...",
  "url": "https://yourapp.com/webhooks/chronith",
  "events": ["booking.created", "booking.confirmed"],
  "isActive": true,
  "createdAt": "2026-01-01T00:00:00Z"
}
```

## Response: Delivery

```json
{
  "id": "3fa85f64-...",
  "webhookId": "3fa85f64-...",
  "event": "booking.confirmed",
  "payload": { "bookingId": "..." },
  "statusCode": 200,
  "attemptCount": 1,
  "succeededAt": "2026-04-15T10:00:01Z",
  "nextRetryAt": null
}
```
