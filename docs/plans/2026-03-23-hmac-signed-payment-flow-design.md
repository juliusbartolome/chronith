# HMAC-Signed Payment Flow — Design Document

**Date:** 2026-03-23
**Status:** Approved
**Branch:** `feat/hmac-signed-payment-flow`

---

## 1. Problem Statement

The current payment flow immediately creates a PayMongo checkout session at booking creation time. This has several issues:

1. **No payment provider choice** — the checkout session is created before the customer can choose a provider (PayMongo vs Maya).
2. **Unsigned return URLs** — the `SuccessUrl`/`FailureUrl` in PayMongo/Maya options use plain `{bookingId}` GUID substitution with no authentication. Anyone who guesses the booking ID can access the success page.
3. **Dashboard dependency** — the success page relies on client-side `sessionStorage` (Zustand). Direct navigation to the success URL after a PayMongo redirect has no booking context.
4. **Legacy factory in public handler** — `PublicCreateBookingHandler` still uses `IPaymentProviderFactory` (global singleton config) instead of `ITenantPaymentProviderResolver` (per-tenant config), which means public bookings cannot use per-tenant payment credentials.

## 2. Solution: Two-Step HMAC-Signed Payment Flow

Replace immediate checkout creation with a two-step flow:

```
Step 1: Booking creation → returns BookingDto with paymentUrl (HMAC-signed magic link)
Step 2: Customer visits payment selection page → picks provider → on-demand checkout session created
```

### Full Flow

```
Customer submits booking
  → API creates booking (PendingPayment, no checkout yet)
  → Returns BookingDto with paymentUrl (HMAC-signed)
  → Frontend redirects to payment selection page

Payment selection page
  → Fetches booking details via HMAC-signed endpoint
  → Shows available payment providers
  → Customer picks PayMongo (or Maya)

On-demand checkout
  → Frontend calls CreatePublicCheckout endpoint (HMAC-authenticated)
  → API creates checkout session via per-tenant resolver
  → Returns checkout URL

Payment provider redirect
  → Customer redirected to checkout.paymongo.com
  → Pays → PayMongo redirects to HMAC-signed success/failure URL

Success page
  → Fetches booking via HMAC-signed verify endpoint (one call, no polling)
  → Webhook arrives async → PendingPayment → PendingVerification
```

## 3. HMAC URL Signing Scheme

### Payload Format

```
booking-access.{bookingId}.{tenantSlug}.{expires}
```

- **Domain prefix:** `booking-access.` — separates from blind-index HMAC usage of the same key.
- **Algorithm:** HMAC-SHA256
- **Key:** Existing `Security:HmacKey` (Base64-encoded 32-byte key, same as blind index but domain-separated)
- **Expiry:** Unix timestamp, 1-hour lifetime
- **Output:** Lowercase hex string

### URL Format

```
https://app.com/pay?bookingId={guid}&tenantSlug={slug}&expires={unix}&sig={hex}
```

### Validation

- Decode `sig` from hex
- Recompute HMAC over `booking-access.{bookingId}.{tenantSlug}.{expires}`
- Compare using `CryptographicOperations.FixedTimeEquals`
- Reject if `expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds()`

## 4. Interface & Service Design

### New Interface (Application Layer)

```csharp
// src/Chronith.Application/Interfaces/IBookingUrlSigner.cs
namespace Chronith.Application.Interfaces;

public interface IBookingUrlSigner
{
    string GeneratePaymentUrl(Guid bookingId, string tenantSlug);
    bool Validate(Guid bookingId, string tenantSlug, long expires, string signature);
}
```

### New Implementation (Infrastructure Layer)

```csharp
// src/Chronith.Infrastructure/Security/HmacBookingUrlSigner.cs
```

- Reuses `Security:HmacKey` via `BlindIndexOptions` (same options class)
- Domain separation via `booking-access.` prefix in the HMAC payload
- 1-hour default lifetime, configurable via `BookingUrlSignerOptions`
- `FixedTimeEquals` for constant-time comparison

## 5. CreateCheckoutRequest Changes

Add optional `SuccessUrl?` and `CancelUrl?` to `CreateCheckoutRequest`:

```csharp
public sealed record CreateCheckoutRequest(
    long AmountInCentavos,
    string Currency,
    string Description,
    Guid BookingId,
    Guid TenantId,
    string? SuccessUrl = null,    // NEW
    string? CancelUrl = null);    // NEW
```

Providers use request-level URLs if provided, otherwise fall back to their options-based URL templates. This is backward compatible — existing callers pass no URLs and get the old behavior.

## 6. ITenantPaymentProviderResolver Changes

Add `ResolveWithContextAsync` returning URL templates alongside the provider:

```csharp
public sealed record ResolvedPaymentContext(
    IPaymentProvider Provider,
    string? SuccessUrlTemplate,
    string? FailureUrlTemplate);

public interface ITenantPaymentProviderResolver
{
    Task<IPaymentProvider?> ResolveAsync(Guid tenantId, string providerName, CancellationToken ct = default);
    Task<ResolvedPaymentContext?> ResolveWithContextAsync(Guid tenantId, string providerName, CancellationToken ct = default);
}
```

## 7. PayMongoOptions / MayaOptions Changes

Add `PaymentPageUrl` to both options classes:

```csharp
// PayMongoOptions
public string PaymentPageUrl { get; set; } = string.Empty;

// MayaOptions
public string PaymentPageUrl { get; set; } = string.Empty;
```

This is the base URL for the payment selection page (e.g., `https://booking.nexoflow.com/pay`). The HMAC-signed query parameters are appended by the signer.

## 8. BookingDto Changes

Add `PaymentUrl?` field (computed in mapper, not persisted):

```csharp
public sealed record BookingDto(
    // ... existing fields ...
    string? PaymentUrl    // NEW — HMAC-signed URL for payment selection page
);
```

The mapper receives the `IBookingUrlSigner` via a new overload `ToDto(booking, signer, tenantSlug)` that generates the URL when `Status == PendingPayment && AmountInCentavos > 0`.

## 9. Command Handler Changes

### CreateBookingHandler (internal, lines 115-145)

- **Remove:** Immediate checkout session creation
- **Add:** Generate HMAC-signed payment URL via `IBookingUrlSigner`
- **Keep:** `ITenantPaymentProviderResolver` dependency (already correct)

### PublicCreateBookingHandler (public, lines 91-106)

- **Remove:** Immediate checkout session creation via `IPaymentProviderFactory`
- **Replace:** `IPaymentProviderFactory` → `ITenantPaymentProviderResolver` (fixes pre-existing gap)
- **Add:** Generate HMAC-signed payment URL via `IBookingUrlSigner`

## 10. New Endpoints

### `POST /v1/public/{tenantSlug}/bookings/{id}/checkout`

- **Auth:** HMAC signature in query params (`bookingId`, `tenantSlug`, `expires`, `sig`)
- **Request body:** `{ "providerName": "PayMongo" }`
- **Response:** `{ "checkoutUrl": "https://checkout.paymongo.com/..." }`
- **Handler:** Creates checkout session on-demand via `ITenantPaymentProviderResolver`
- Stores `CheckoutUrl` + `PaymentReference` on the booking

### `GET /v1/public/{tenantSlug}/bookings/{id}/verify`

- **Auth:** HMAC signature in query params
- **Response:** `PublicBookingStatusDto`
- **Purpose:** Payment success/failure page fetches booking details with one authenticated call

## 11. Files Changed Summary

### New Files

| File                                                                      | Layer          | Purpose                     |
| ------------------------------------------------------------------------- | -------------- | --------------------------- |
| `src/Chronith.Application/Interfaces/IBookingUrlSigner.cs`                | Application    | Interface                   |
| `src/Chronith.Infrastructure/Security/HmacBookingUrlSigner.cs`            | Infrastructure | HMAC implementation         |
| `src/Chronith.Application/Commands/Public/CreatePublicCheckoutCommand.cs` | Application    | On-demand checkout command  |
| `src/Chronith.Application/Queries/Public/GetVerifiedBookingQuery.cs`      | Application    | HMAC-verified booking query |
| `src/Chronith.API/Endpoints/Public/PublicCreateCheckoutEndpoint.cs`       | API            | Checkout endpoint           |
| `src/Chronith.API/Endpoints/Public/PublicVerifyBookingEndpoint.cs`        | API            | Verify endpoint             |

### Modified Files

| File                                                                     | Change                                                     |
| ------------------------------------------------------------------------ | ---------------------------------------------------------- |
| `src/Chronith.Application/DTOs/PaymentDtos.cs`                           | Add `SuccessUrl?`, `CancelUrl?` to `CreateCheckoutRequest` |
| `src/Chronith.Application/DTOs/BookingDto.cs`                            | Add `PaymentUrl?` field                                    |
| `src/Chronith.Application/Mappers/BookingMapper.cs`                      | New overload with signer                                   |
| `src/Chronith.Application/Interfaces/ITenantPaymentProviderResolver.cs`  | Add `ResolveWithContextAsync`                              |
| `src/Chronith.Application/Commands/Bookings/CreateBookingCommand.cs`     | Skip checkout, generate payment URL                        |
| `src/Chronith.Application/Commands/Public/PublicCreateBookingCommand.cs` | Migrate to resolver, generate payment URL                  |
| `src/Chronith.Infrastructure/Payments/TenantPaymentProviderResolver.cs`  | Implement `ResolveWithContextAsync`                        |
| `src/Chronith.Infrastructure/Payments/PayMongo/PayMongoProvider.cs`      | Use request URLs if provided                               |
| `src/Chronith.Infrastructure/Payments/PayMongo/PayMongoOptions.cs`       | Add `PaymentPageUrl`                                       |
| `src/Chronith.Infrastructure/Payments/MayaProvider.cs`                   | Use request URLs if provided                               |
| `src/Chronith.Infrastructure/Payments/MayaOptions.cs`                    | Add `PaymentPageUrl`                                       |
| `src/Chronith.Infrastructure/DependencyInjection.cs`                     | Register `IBookingUrlSigner`                               |
| `src/Chronith.Infrastructure/Payments/StubPaymentProvider.cs`            | Support optional URLs                                      |

### Test Files

| File                                                                               | Type       |
| ---------------------------------------------------------------------------------- | ---------- |
| `tests/Chronith.Tests.Unit/Infrastructure/Security/HmacBookingUrlSignerTests.cs`   | Unit       |
| `tests/Chronith.Tests.Unit/Application/CreatePublicCheckoutCommandHandlerTests.cs` | Unit       |
| `tests/Chronith.Tests.Unit/Application/GetVerifiedBookingQueryHandlerTests.cs`     | Unit       |
| `tests/Chronith.Tests.Functional/Payments/PublicCreateCheckoutEndpointTests.cs`    | Functional |
| `tests/Chronith.Tests.Functional/Payments/PublicVerifyBookingEndpointTests.cs`     | Functional |
| Updates to existing handler/provider tests                                         | Unit       |
