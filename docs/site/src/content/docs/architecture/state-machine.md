---
title: Booking State Machine
description: The Chronith booking lifecycle and valid state transitions.
---

## State diagram

```
                    [Create booking]
                          │
                          ▼
              ┌─────────────────────┐
              │   PendingPayment    │  ◄── Skipped if free (price = 0)
              └─────────────────────┘
                    │         │
               [Pay]         [Cancel]
                    │         │
                    ▼         ▼
              ┌──────────────────────┐   ┌─────────────┐
              │ PendingVerification  │   │  Cancelled  │
              └──────────────────────┘   └─────────────┘
                    │         │
             [Confirm]       [Cancel]
                    │         │
                    ▼         ▼
              ┌───────────┐  ┌─────────────┐
              │ Confirmed │  │  Cancelled  │
              └───────────┘  └─────────────┘
                    │
                [Cancel]
                    │
                    ▼
              ┌─────────────┐
              │  Cancelled  │
              └─────────────┘
```

## Valid transitions

| From | To | Trigger | Notes |
|------|----|---------|-------|
| *(new)* | `PendingPayment` | `Create` | Only if `priceCentavos > 0` |
| *(new)* | `PendingVerification` | `Create` | Only if `priceCentavos == 0` |
| `PendingPayment` | `PendingVerification` | `Pay` | Payment received |
| `PendingPayment` | `Cancelled` | `Cancel` | Customer or admin |
| `PendingVerification` | `Confirmed` | `Confirm` | Staff confirms |
| `PendingVerification` | `Cancelled` | `Cancel` | Staff or admin rejects |
| `Confirmed` | `Cancelled` | `Cancel` | Admin cancels |

## Domain enforcement

State transitions are enforced at the domain level. Attempting an invalid transition throws `InvalidStateTransitionException`:

```
HTTP 422 Unprocessable Entity

{
  "type": "https://chronith.io/errors/invalid-state-transition",
  "title": "Invalid State Transition",
  "status": 422,
  "detail": "Cannot confirm a booking that is in Cancelled status."
}
```

There is no direct path from `Confirmed` back to `PendingVerification`. Once confirmed, a booking can only be cancelled or rescheduled (which creates a new booking in `PendingPayment`/`PendingVerification`).
