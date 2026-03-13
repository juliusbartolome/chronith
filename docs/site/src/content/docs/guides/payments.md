---
title: Payments
description: Configure and handle payments in Chronith.
---

Chronith uses a pluggable `IPaymentProvider` interface, supporting a `Stub` provider for development and `PayMongo` for production.

## Payment flow

```
Customer creates booking
        ↓
 PendingPayment (skip if free)
        ↓
 Customer completes payment
        ↓
 PendingVerification
        ↓
 Staff confirms → Confirmed
```

**Free bookings** (price = 0) skip `PendingPayment` entirely and go straight to `PendingVerification`.

## PayMongo configuration

Set your provider to `PayMongo` and provide keys:

| Variable | Description |
|----------|-------------|
| `Payments__Provider` | Set to `PayMongo` |
| `Payments__PayMongo__SecretKey` | Your PayMongo secret key |
| `Payments__PayMongo__PublicKey` | Your PayMongo public key |

## Stub provider

The `Stub` provider automatically marks payments as received without processing real payments. It is suitable only for development and testing.

**Warning:** Never use the Stub provider in production.

## Initiate payment

```sh
POST /v1/bookings/{id}/payment/initiate
Authorization: Bearer <token>
```

Returns a payment URL or intent that the customer uses to complete payment.

## Payment webhook

PayMongo sends payment events to:

```
POST /v1/webhooks/paymongo
```

This endpoint is handled automatically by Chronith.

## Refunds

```sh
POST /v1/bookings/{id}/refund
Authorization: Bearer <token>
```
