# Webhook Event Subscriptions — Design

## Problem

Outbound tenant webhooks currently fire for **every** booking status change. Tenants cannot control which events a given webhook endpoint receives. If a tenant only cares about `booking.confirmed`, they still get `booking.cancelled`, `booking.payment_received`, and `booking.payment_failed` delivered to the same URL.

## Goal

Allow tenants to specify which webhook event names each registered webhook should receive. Only matching events are dispatched.

## Scope

- **Tenant webhooks only.** Customer callback delivery (single URL on booking type) is unchanged.
- **Outbound filtering only.** Inbound payment provider webhooks are unaffected.

## Known Webhook Event Names

| Event Name                 | Triggered When                               |
| -------------------------- | -------------------------------------------- |
| `booking.payment_received` | Booking transitions to `PendingVerification` |
| `booking.confirmed`        | Booking transitions to `Confirmed`           |
| `booking.cancelled`        | Booking transitions to `Cancelled`           |
| `booking.payment_failed`   | Booking transitions to `PaymentFailed`       |

## Design Decisions

| Decision                          | Choice                  | Rationale                                                                |
| --------------------------------- | ----------------------- | ------------------------------------------------------------------------ |
| Filter key                        | Webhook event names     | Matches what is already sent in outbound payloads; decoupled from enum   |
| Persistence                       | Separate join table     | Normalized; queryable; extensible if metadata per subscription is needed |
| Backward compatibility            | Default to all 4 events | Existing webhooks keep current fire-all behavior after migration         |
| Update support                    | PATCH endpoint          | Tenants can change subscriptions without delete + re-create              |
| Customer callback event filtering | Not in scope            | Single URL per booking type; no registration model to extend             |

## Domain Model

`Webhook` gains a private `List<string>` backing field for subscribed event names, exposed as `IReadOnlyList<string> EventTypes`.

- `Webhook.Create(...)` requires a non-empty event type list. Rejects unknown names.
- `Webhook.UpdateSubscriptions(IReadOnlyList<string> eventTypes)` replaces the list. Same validation.
- `Webhook.Update(string? url, string? secret, IReadOnlyList<string>? eventTypes)` for partial updates.

Allowed event names are defined as constants in the domain (e.g., `WebhookEventTypes.BookingConfirmed`).

## Persistence

### New table: `webhook_event_subscriptions`

| Column      | Type           | Notes                    |
| ----------- | -------------- | ------------------------ |
| `Id`        | `uuid` PK      |                          |
| `WebhookId` | `uuid` FK      | References `webhooks.Id` |
| `EventName` | `varchar(100)` | e.g. `booking.confirmed` |

Unique index on `(WebhookId, EventName)`.

### Entity

`WebhookEventSubscriptionEntity` with `Id`, `WebhookId`, `EventName`.

Navigation property on `WebhookEntity`: `List<WebhookEventSubscriptionEntity> EventSubscriptions`.

### Repository

- Reads: `Include(w => w.EventSubscriptions)` on all webhook queries.
- Writes (add): insert subscription rows alongside the webhook.
- Writes (update): `ExecuteDeleteAsync` existing subscriptions + `AddRangeAsync` new ones (replace pattern used elsewhere in the codebase).

### Migration

Data migration inserts all 4 event names for every existing webhook row.

## API Surface

### Create Webhook

`POST /v1/booking-types/{slug}/webhooks`

```json
{
  "url": "https://example.com/hook",
  "secret": "my-secret-at-least-16-chars",
  "eventTypes": ["booking.confirmed", "booking.cancelled"]
}
```

Validation:

- `eventTypes` required, non-empty
- Each entry must be a known event name
- Duplicates rejected

### Update Webhook

`PATCH /v1/booking-types/{slug}/webhooks/{webhookId}`

```json
{
  "url": "https://example.com/hook-v2",
  "eventTypes": ["booking.confirmed", "booking.payment_failed"]
}
```

All fields optional. Only provided fields are updated. `secret` can also be updated.

### List Webhooks

`GET /v1/booking-types/{slug}/webhooks`

Response now includes `eventTypes`:

```json
[
  {
    "id": "...",
    "url": "https://example.com/hook",
    "eventTypes": ["booking.confirmed", "booking.cancelled"]
  }
]
```

### WebhookDto

```csharp
public sealed record WebhookDto(Guid Id, string Url, IReadOnlyList<string> EventTypes);
```

## Dispatch Flow

`WebhookOutboxHandler.Handle(...)`:

1. Resolve `tenantEventType` from `notification.ToStatus` (unchanged).
2. Fetch all webhooks for the booking type (unchanged query).
3. **Filter** to only webhooks whose `EventTypes` contains `tenantEventType`.
4. Create outbox entries for the filtered set only.

No change to `WebhookDispatcherService` — it processes outbox entries as before.

## Testing

| Layer       | What to test                                                              |
| ----------- | ------------------------------------------------------------------------- |
| Unit        | `Webhook.Create` rejects empty/unknown event types                        |
| Unit        | `Webhook.UpdateSubscriptions` replaces list, validates                    |
| Unit        | `CreateWebhookValidator` rejects invalid event names                      |
| Unit        | `WebhookOutboxHandler` filters webhooks by subscribed event               |
| Unit        | `WebhookOutboxHandler` skips webhooks not subscribed to the emitted event |
| Integration | Repository round-trip: create webhook with subscriptions, read back       |
| Functional  | Create webhook with event types via API, list and verify                  |
| Functional  | Update webhook subscriptions via PATCH, list and verify                   |
| Functional  | Auth tests for new PATCH endpoint                                         |
