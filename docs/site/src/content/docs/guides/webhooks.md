---
title: Webhooks
description: Register and manage webhooks in Chronith.
---

Webhooks allow you to receive real-time notifications when events occur in Chronith.

## Register a webhook

```sh
POST /v1/webhooks
Authorization: Bearer <token>
```

```json
{
  "url": "https://yourapp.com/webhooks/chronith",
  "events": ["booking.created", "booking.confirmed", "booking.cancelled"],
  "secret": "your-webhook-secret"
}
```

## Available events

| Event | Description |
|-------|-------------|
| `booking.created` | A new booking has been created |
| `booking.confirmed` | A booking has been confirmed |
| `booking.cancelled` | A booking has been cancelled |
| `booking.rescheduled` | A booking has been rescheduled |
| `payment.received` | Payment has been received for a booking |

## Delivery and retry

Webhooks use the **outbox pattern**: events are first persisted to the database, then dispatched asynchronously.

- **Retries:** Up to 5 attempts per event
- **Backoff:** Exponential (1s, 2s, 4s, 8s, 16s)
- **Timeout:** 30 seconds per attempt

## Signature verification

Each delivery includes an `X-Chronith-Signature` header. Verify it to ensure the request is from Chronith:

```js
const crypto = require('crypto');

function verifySignature(payload, signature, secret) {
  const expected = crypto
    .createHmac('sha256', secret)
    .update(payload)
    .digest('hex');
  return crypto.timingSafeEqual(
    Buffer.from(signature, 'hex'),
    Buffer.from(expected, 'hex')
  );
}
```

## List webhooks

```sh
GET /v1/webhooks
Authorization: Bearer <token>
```

## View delivery history

```sh
GET /v1/webhooks/{id}/deliveries
Authorization: Bearer <token>
```

## Delete a webhook

```sh
DELETE /v1/webhooks/{id}
Authorization: Bearer <token>
```
