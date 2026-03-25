# Payment Failed Status, Pipeline Logging & Dashboard Pay Button

**Date:** 2026-03-26
**Status:** Implemented

---

## Problem

Three gaps in the current payment flow:

1. **No PaymentFailed status.** When PayMongo sends a `payment.failed` webhook, the handler silently ignores it. The booking stays in `PendingPayment` indefinitely with no record of the failure. Admins have no visibility into failed payments.

2. **Zero logging in the payment pipeline.** The entire flow — webhook receipt, signature validation, event parsing, booking lookup, state transitions, checkout creation, provider resolution — has no domain-specific logging. The only logging is a generic MediatR `"Handling {RequestName}"` / `"Handled {RequestName}"` from the `LoggingBehavior`.

3. **No "Mark as Paid" button in the dashboard.** The API supports manual payment (`POST /bookings/{id}/pay`) but the dashboard doesn't expose it. Admins can only Cancel a `PendingPayment` booking from the UI.

---

## Design

### Feature 1: PaymentFailed Status (Terminal)

#### State Machine (Updated — Split Payment Flow)

Two payment paths:

```
AUTOMATIC (webhook from PayMongo/GCash/Maya — trusted source):
  PendingPayment ──ConfirmPayment()──> Confirmed

MANUAL (admin "Mark as Paid" — needs staff verification):
  PendingPayment ──Pay()──> PendingVerification ──Confirm()──> Confirmed

FAILURE:
  PendingPayment ──FailPayment()──> PaymentFailed (terminal)

CANCEL:
  PendingPayment/PendingVerification/Confirmed ──Cancel()──> Cancelled

FREE BOOKINGS:
  Created directly in Confirmed status (skip PendingPayment entirely)
```

`PaymentFailed` is a **terminal state** like `Cancelled`. No transitions out. The customer must create a new booking.

**Rationale for split flow:** `PendingVerification` only makes sense for manual payment flows where a customer claims they paid and staff needs to verify. For automated payments where the gateway confirms via webhook, going through PendingVerification is unnecessary — the payment is already verified by the trusted source.

**Rationale for free bookings starting Confirmed:** Nothing to pay = nothing to verify. Free bookings skip the payment pipeline entirely.

#### Domain Changes

- **`BookingStatus` enum:** Add `PaymentFailed` (ordinal 4).
- **`Booking.ConfirmPayment(string changedById, string changedByRole)`:** New method. Guard: `Status == PendingPayment`. Transition directly to `Confirmed`. Used by webhook handler for trusted automated payments.
- **`Booking.FailPayment(string changedById, string changedByRole)`:** New method. Guard: `Status == PendingPayment`. Transition to `PaymentFailed`.
- **`Booking.Create()`:** Free bookings (amount=0) now start at `Confirmed` instead of `PendingVerification`.
- **`Booking.Cancel()`:** Also reject `PaymentFailed` (already terminal).
- **`Booking.AssignStaff()` / `Booking.Reschedule()`:** Also reject `PaymentFailed`.

#### Application Changes

- **`ProcessPaymentWebhookHandler`:** On `PaymentEventType.Success`, call `booking.ConfirmPayment()` (automated path → Confirmed). On `PaymentEventType.Failed`, call `booking.FailPayment()`. Both paths publish `BookingStatusChangedNotification`.
- **`ConflictStatuses` arrays:** No change needed — `PaymentFailed` bookings should NOT block slots. The existing arrays explicitly list `PendingPayment`, `PendingVerification`, `Confirmed` — any status not in the list is already excluded.
- **Webhook/notification outbox handlers:** Add `PaymentFailed` case mapping to event types:
  - Tenant webhook: `booking.payment_failed`
  - Customer callback: `customer.payment.failed`
  - Notification: `notification.payment_failed`
- **Checkout guard:** `CreatePublicCheckoutCommand` already rejects non-`PendingPayment` bookings, so `PaymentFailed` bookings are naturally rejected.

#### Infrastructure Changes

- **EF migration:** No schema change needed. `Status` column is `string(30)` — `"PaymentFailed"` (13 chars) fits. The EF `HasConversion<string>()` handles it automatically.
- **Entity mapper:** No change — `BookingStatus` is mapped via string conversion.

#### Dashboard Changes

- **Status badge:** Add `PaymentFailed` variant (destructive/red, like `Cancelled`).

#### SDK Changes

- **TypeScript SDK:** Types are auto-generated from OpenAPI — picks up `PaymentFailed` on next regeneration.
- **C# SDK:** Status is stored as `string` — no enum exists. `"PaymentFailed"` works automatically.

### Feature 2: Payment Pipeline Logging

Add structured `ILogger<T>` logging to all payment-flow components. Use Serilog structured logging with named properties for correlation.

| Component                       | Log Level   | What                                                                                           |
| ------------------------------- | ----------- | ---------------------------------------------------------------------------------------------- |
| `PaymentWebhookEndpoint`        | Information | Webhook received: `{TenantId}`, `{Provider}`, `{SourceIp}`                                     |
| `ProcessPaymentWebhookHandler`  | Information | Provider resolved for `{TenantId}/{Provider}`                                                  |
|                                 | Warning     | Webhook signature validation failed for `{TenantId}/{Provider}`                                |
|                                 | Information | Parsed event: `{EventType}`, `{ProviderTransactionId}`                                         |
|                                 | Information | Booking found: `{BookingId}`, current status `{FromStatus}`                                    |
|                                 | Information | State transition: `{BookingId}` `{FromStatus}` -> `{ToStatus}`                                 |
|                                 | Warning     | Non-success event `{EventType}` for `{ProviderTransactionId}` — transitioning to PaymentFailed |
|                                 | Warning     | Booking not found for payment reference `{ProviderTransactionId}`                              |
| `PayMongoProvider`              | Warning     | Missing `paymongo-signature` header                                                            |
|                                 | Warning     | Webhook timestamp outside tolerance: `{TimestampDelta}s`                                       |
|                                 | Warning     | Webhook signature mismatch (no secrets logged)                                                 |
|                                 | Debug       | Unknown event type: `{EventType}`                                                              |
|                                 | Information | Checkout session created: `{CheckoutUrl}` (truncated)                                          |
|                                 | Error       | PayMongo API error: `{StatusCode}` `{ErrorBody}`                                               |
| `TenantPaymentProviderResolver` | Information | Resolved `{ProviderName}` for tenant `{TenantId}`                                              |
|                                 | Warning     | No active payment config for `{TenantId}/{ProviderName}`                                       |
|                                 | Warning     | Unknown provider name: `{ProviderName}`                                                        |
| `CreatePublicCheckoutHandler`   | Information | Creating checkout for `{BookingId}`, provider `{ProviderName}`                                 |
|                                 | Information | URL resolution: using `{Tier}` (request/tenant-config/global)                                  |
|                                 | Information | Checkout session created, redirecting to `{Provider}`                                          |

### Feature 3: Dashboard "Mark as Paid" Button

- **API route:** `POST /api/bookings/[id]/pay/route.ts` — proxies to Chronith `POST /v1/bookings/{id}/pay` with JWT auth.
- **Hook:** Add `usePayBooking` mutation to `use-bookings.ts` — same pattern as `useConfirmBooking`.
- **UI:** Add "Mark as Paid" button on booking detail page, visible when `status === "PendingPayment"`. Placed next to the existing Cancel button.

---

## Files Affected

### Domain

- `src/Chronith.Domain/Enums/BookingStatus.cs`
- `src/Chronith.Domain/Models/Booking.cs`

### Application

- `src/Chronith.Application/Commands/Bookings/ProcessPaymentWebhookCommand.cs`
- `src/Chronith.Application/Notifications/WebhookOutboxHandler.cs`
- `src/Chronith.Application/Notifications/NotificationOutboxHandler.cs`
- `src/Chronith.Application/Commands/Public/CreatePublicCheckoutCommand.cs`

### Infrastructure

- `src/Chronith.Infrastructure/Payments/PayMongo/PayMongoProvider.cs`
- `src/Chronith.Infrastructure/Payments/TenantPaymentProviderResolver.cs`

### API

- `src/Chronith.API/Endpoints/Payments/PaymentWebhookEndpoint.cs`

### Dashboard

- `dashboard/src/components/bookings/booking-status-badge.tsx`
- `dashboard/src/app/(dashboard)/bookings/[id]/page.tsx`
- `dashboard/src/hooks/use-bookings.ts`
- `dashboard/src/app/api/bookings/[id]/pay/route.ts` (NEW)

### C# SDK

- `packages/sdk-csharp/src/Chronith.Client/Models/BookingDto.cs` — Status is `string`, no enum. No change needed.

### Tests

- Unit tests for `FailPayment()`, updated `Cancel()` guard, `ProcessPaymentWebhookHandler` failed event path
- Unit tests for logging (verify log calls in handler)
- Update existing tests that enumerate `BookingStatus` values
