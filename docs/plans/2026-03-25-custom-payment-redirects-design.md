# Custom Payment Redirect URLs

**Date:** 2026-03-25
**Branch:** `feat/custom-payment-redirects`
**Status:** Approved

## Problem

After a customer completes (or cancels) payment via PayMongo/Maya, they are redirected to a hardcoded URL derived from the global `PaymentPage:BaseUrl` config (`https://example.com/pay/success` or `https://example.com/pay/failed`). Tenants cannot customize where their customers land after payment.

## Solution

Add `PaymentSuccessUrl` and `PaymentFailureUrl` fields to `TenantPaymentConfig`, with optional per-request overrides on the checkout endpoint.

### URL Resolution Order

```
1. Per-request override (SuccessUrl/FailureUrl on checkout POST body)
2. TenantPaymentConfig.PaymentSuccessUrl / PaymentFailureUrl
3. Global PaymentPageOptions.BaseUrl + "/success" or "/failed"
```

HMAC query params (`bookingId`, `tenantSlug`, `expires`, `sig`) are always appended to whichever URL is resolved. The HMAC payload is unchanged (`booking-access.{bookingId}.{tenantSlug}.{expires}`).

## Layer Changes

### Domain

`TenantPaymentConfig` — add two nullable string properties:

- `PaymentSuccessUrl` (`string?`)
- `PaymentFailureUrl` (`string?`)

Update `Create()` factory and `UpdateDetails()` method signatures.

### Infrastructure

- **Entity:** Add `PaymentSuccessUrl` and `PaymentFailureUrl` to `TenantPaymentConfigEntity`.
- **Configuration:** Two new `varchar(2048)` nullable columns: `payment_success_url`, `payment_failure_url`.
- **Entity Mapper:** Map the two new fields in `ToDomain()` and `ToEntity()`.
- **Migration:** `AddColumn` only — existing rows get `null` (falls back to global config).

### Application

- **DTOs:** Add fields to `TenantPaymentConfigDto`. No change to `PaymentProviderSummaryDto`.
- **Commands:**
  - `CreateTenantPaymentConfigCommand` — add optional URL fields + `Must(BeAbsoluteHttpsUri)` validation.
  - `UpdateTenantPaymentConfigCommand` — add optional URL fields + same validation.
  - `CreatePublicCheckoutCommand` — add optional `SuccessUrl`/`FailureUrl` + same validation.
- **Handler (`CreatePublicCheckoutHandler`):**
  - Load the `TenantPaymentConfig` for the resolved provider to access URL fields.
  - Apply resolution order: request > config > global fallback.
  - Pass resolved URL to `IBookingUrlSigner.GenerateSignedUrl()` as today.

### API

- `CreateTenantPaymentConfigRequest` — add `PaymentSuccessUrl?`, `PaymentFailureUrl?`.
- `UpdateTenantPaymentConfigRequest` — add `PaymentSuccessUrl?`, `PaymentFailureUrl?`.
- `PublicCreateCheckoutRequest` — add `SuccessUrl?`, `FailureUrl?` (body fields).

### Dashboard

- Tenant payment config form: add two URL input fields with placeholder text.

### Unchanged

- `IBookingUrlSigner` / `HmacBookingUrlSigner` — unchanged.
- `PayMongoProvider` / `MayaProvider` — unchanged (already accept `SuccessUrl`/`CancelUrl`).
- `CreateCheckoutRequest` DTO — unchanged (already has optional fields).
- `PaymentPageOptions` — stays as global fallback.
- `BookingDto.PaymentUrl` — unchanged (payment selection page, not post-payment redirect).

## Testing

- **Unit:** `CreatePublicCheckoutHandler` resolution order (request > config > global), URL validation on commands, `TenantPaymentConfig` domain model create/update with URLs.
- **Functional:** Full checkout flow with custom URLs.
