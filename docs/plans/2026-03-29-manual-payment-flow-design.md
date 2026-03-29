# Manual Payment Flow — Design Document

**Date:** 2026-03-29
**Status:** Draft

---

## Problem

Chronith already supports `PaymentMode.Manual` on booking types and `TenantPaymentConfig` with `ProviderName = "Manual"` (storing `QrCodeUrl` and `PublicNote`). However, the end-to-end flow is incomplete:

1. **No payment page.** The HMAC-signed `paymentUrl` returned after booking creation points to a placeholder `https://example.com/pay`. Customers with Manual bookings see no instructions.

2. **`CreatePublicCheckoutCommand` throws for Manual.** The `TenantPaymentProviderResolver` returns `null` for `"Manual"` providers, causing a `NotFoundException`. Customers cannot initiate manual payment.

3. **`PublicBookingStatusDto` lacks manual payment context.** No `PublicNote`, `QrCodeUrl`, `PaymentMode`, or proof-of-payment fields are exposed. The public booking status page is useless for Manual payment bookings.

4. **No customer "I've paid" endpoint.** Only admin/staff can call `PayBookingCommand` (JWT-authenticated). Customers have no way to self-report payment and upload proof.

5. **No proof-of-payment upload infrastructure.** Zero file upload capability exists — no Azure Blob SDK, no storage interfaces, no upload endpoints.

6. **No staff verification via HMAC links.** Staff can only verify payments through the dashboard (JWT auth). No mobile-friendly verification link exists for on-the-go staff.

7. **Success page is static.** Always displays "Confirmed!" regardless of booking status. No conditional rendering for `PendingPayment` or `PendingVerification` states.

---

## Design

### 1. Azure Blob Storage Infrastructure

Greenfield file upload capability using Azure Blob Storage.

**Interface:**

```csharp
public interface IFileStorageService
{
    Task<FileUploadResult> UploadAsync(
        string containerName,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken ct = default);

    Task<Stream?> DownloadAsync(
        string containerName,
        string fileName,
        CancellationToken ct = default);

    Task DeleteAsync(
        string containerName,
        string fileName,
        CancellationToken ct = default);
}

public sealed record FileUploadResult(string Url, string FileName);
```

**Implementation:** `AzureBlobStorageService` using `Azure.Storage.Blobs` SDK. Container per tenant slug: `payment-proofs-{tenantSlug}`. Blob naming: `{bookingId}/{timestamp}-{sanitizedFileName}`.

**Options:**

```csharp
public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";
    public string ConnectionString { get; set; } = string.Empty;
}
```

**Local development:** Azurite container in `docker-compose.yml`, using `UseDevelopmentStorage=true` connection string.

### 2. Domain Model Changes

Add proof-of-payment fields to `Booking`:

```csharp
// New properties on Booking
public string? ProofOfPaymentUrl { get; private set; }
public string? ProofOfPaymentFileName { get; private set; }
public string? PaymentNote { get; private set; }

// New domain method
public void SubmitProofOfPayment(
    string? proofUrl,
    string? proofFileName,
    string? paymentNote,
    string changedById,
    string changedByRole)
{
    if (Status != BookingStatus.PendingPayment)
        throw new InvalidStateTransitionException(Status, "submit proof of payment");

    ProofOfPaymentUrl = proofUrl;
    ProofOfPaymentFileName = proofFileName;
    PaymentNote = paymentNote;

    // Transition to PendingVerification
    Transition(BookingStatus.PendingVerification, changedById, changedByRole);
}
```

This method combines proof attachment + state transition into a single atomic operation. The booking moves from `PendingPayment` to `PendingVerification` when the customer submits proof.

**State machine (Manual payment path):**

```
PendingPayment ──SubmitProofOfPayment()──> PendingVerification ──Confirm()──> Confirmed
```

### 3. New Endpoints

#### 3a. Customer Confirm Manual Payment

`POST /v1/public/{tenantSlug}/bookings/{bookingId}/confirm-payment`

**Auth:** HMAC-signed URL (same mechanism as payment page access). The request includes `bookingId`, `tenantSlug`, `expires`, and `sig` in query params.

**Request:** Multipart form data:

- `proofFile` (optional) — image file (JPEG, PNG, WebP; max 5 MB)
- `paymentNote` (optional) — text note from customer (max 500 chars)

**Behavior:**

1. Validate HMAC signature (reuse `IBookingUrlSigner.Validate`)
2. Upload proof file to Azure Blob Storage (if provided)
3. Call `booking.SubmitProofOfPayment()` — transitions to `PendingVerification`
4. Publish `BookingStatusChangedNotification`
5. Return updated `PublicBookingStatusDto`

#### 3b. Staff Verify Payment (HMAC Link)

`POST /v1/public/{tenantSlug}/bookings/{bookingId}/staff-verify`

**Auth:** HMAC-signed URL with a separate domain prefix (`staff-verify` vs `booking-access`). Different signing domain prevents customers from crafting staff verification links.

**Request body (JSON):**

- `action` — `"approve"` or `"reject"`
- `note` (optional) — staff note

**Behavior:**

- `approve`: calls `booking.Confirm()` — transitions `PendingVerification` → `Confirmed`
- `reject`: calls `booking.Cancel()` — transitions `PendingVerification` → `Cancelled`
- Publishes `BookingStatusChangedNotification`

**URL generation:** Add `GenerateStaffVerifyUrl` to `IBookingUrlSigner`:

```csharp
string GenerateStaffVerifyUrl(string baseUrl, Guid bookingId, string tenantSlug);
bool ValidateStaffVerify(Guid bookingId, string tenantSlug, long expires, string signature);
```

### 4. Enriched `PublicBookingStatusDto`

Add manual payment context fields:

```csharp
public sealed record PublicBookingStatusDto(
    Guid Id,
    string ReferenceId,
    BookingStatus Status,
    DateTimeOffset Start,
    DateTimeOffset End,
    long AmountInCentavos,
    string Currency,
    string? PaymentReference,
    string? CheckoutUrl,
    // New fields
    string? PaymentMode,              // "Manual" | "Automatic" | null
    ManualPaymentOptionsDto? ManualPaymentOptions,
    string? ProofOfPaymentUrl,
    string? ProofOfPaymentFileName,
    string? PaymentNote
);

public sealed record ManualPaymentOptionsDto(
    string? QrCodeUrl,
    string? PublicNote,
    string Label
);
```

Both `GetPublicBookingStatusQuery` and `GetVerifiedBookingQuery` handlers need to:

1. Load the booking's `BookingType` to get `PaymentMode`
2. If `PaymentMode == Manual`, load the active `TenantPaymentConfig` for provider `"Manual"` to get `QrCodeUrl` and `PublicNote`
3. Map proof fields from the booking

### 5. Notification Updates

When a booking transitions to `PendingVerification` (customer submitted proof), notifications sent to staff should include an HMAC-signed staff verification URL. This allows staff to approve/reject directly from the notification (email or SMS) without logging into the dashboard.

The `WebhookOutboxHandler` and notification channel handlers need to include `staffVerifyUrl` in the notification payload when `ToStatus == PendingVerification`.

### 6. Dashboard Changes

#### 6a. Payment Page (`/book/{tenantSlug}/{btSlug}/pay`)

New page accessed via the HMAC-signed `paymentUrl`. Displays:

- Booking summary (service, date/time, amount)
- QR code image (from `ManualPaymentOptions.QrCodeUrl`)
- Payment instructions (from `ManualPaymentOptions.PublicNote`)
- File upload area for proof of payment (drag-and-drop + click)
- Optional payment note text field
- "I've sent my payment" submit button

On submit, calls `POST /v1/public/{tenantSlug}/bookings/{bookingId}/confirm-payment` with the multipart form data. On success, redirects to the success page.

#### 6b. Updated Success Page

Conditional rendering based on booking status:

- `Confirmed` → "Your booking is confirmed!" (existing behavior)
- `PendingVerification` → "Payment submitted! Waiting for staff verification."
- `PendingPayment` → "Please complete your payment." with link to payment page

#### 6c. Staff Verification Page (`/verify/{tenantSlug}/{bookingId}`)

Accessed via the HMAC-signed staff verification URL. Displays:

- Booking details (customer name, service, date/time, amount)
- Proof of payment image (if uploaded)
- Payment note (if provided)
- "Approve" and "Reject" buttons

On approve, calls `POST /v1/public/{tenantSlug}/bookings/{bookingId}/staff-verify` with `action: "approve"`.
On reject, prompts for a note and calls with `action: "reject"`.

---

## Security Considerations

1. **HMAC domain separation.** Customer access uses `booking-access` domain prefix; staff verification uses `staff-verify` domain prefix. This prevents customers from constructing staff verification URLs.

2. **File upload validation.** Only allow JPEG, PNG, WebP. Max 5 MB. Validate content type and file size server-side. Sanitize file names.

3. **Blob storage access.** Blobs are stored with public read access (for QR display in dashboard) but write access is only through the API. Container-level access policy.

4. **HMAC token lifetime.** Payment page tokens use the existing `PaymentPageOptions.TokenLifetimeSeconds` (default 1 hour). Staff verification tokens use a longer lifetime (24 hours) since staff may not check immediately.

---

## Out of Scope

- Multiple proof-of-payment uploads (one file per booking is sufficient for v1)
- Proof file re-upload after initial submission
- Customer-facing payment method selection page (Manual is the only option shown)
- Automated payment matching/reconciliation
- Refund flows for Manual payments
