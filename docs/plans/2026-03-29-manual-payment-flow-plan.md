# Manual Payment Flow — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Build a complete manual payment flow: customers see payment instructions (QR code, bank details) on a public payment page, upload proof of payment, and staff verify payments via HMAC-signed links or the dashboard.

**Architecture:** Bottom-up implementation (Infrastructure → Domain → Application → API → Dashboard). All backend tasks follow strict TDD.

**Tech Stack:** .NET 10, FastEndpoints, MediatR, FluentValidation, EF Core + Npgsql, Azure Blob Storage, xUnit, FluentAssertions, NSubstitute, Next.js (dashboard), TanStack Query.

**Design doc:** `docs/plans/2026-03-29-manual-payment-flow-design.md`

---

## Pre-flight: Branch Setup

```bash
git checkout main && git pull
git checkout -b feat/manual-payment-flow
# Copy design doc + plan
git add docs/plans/2026-03-29-manual-payment-flow-design.md docs/plans/2026-03-29-manual-payment-flow-plan.md
git commit -m "docs: add manual payment flow design and plan"
```

---

## Task 1: Azure Blob Storage Infrastructure

**Layer:** Application (interface) + Infrastructure (implementation) + API (DI)

### 1a. NuGet Package

Add `Azure.Storage.Blobs` (latest stable) to `Chronith.Infrastructure.csproj`.

### 1b. Application Interface

Create `src/Chronith.Application/Interfaces/IFileStorageService.cs`:

```csharp
namespace Chronith.Application.Interfaces;

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

### 1c. Options Class

Create `src/Chronith.Application/Options/BlobStorageOptions.cs`:

```csharp
namespace Chronith.Application.Options;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";
    public string ConnectionString { get; set; } = string.Empty;
}
```

### 1d. Implementation

Create `src/Chronith.Infrastructure/Storage/AzureBlobStorageService.cs`:

- Constructor: `BlobServiceClient` from options connection string
- `UploadAsync`: creates container if not exists, uploads blob with content type, returns public URL + file name
- `DownloadAsync`: downloads blob to stream, returns null if not found
- `DeleteAsync`: deletes blob, no-op if not found
- Container names: lowercase, alphanumeric + hyphens
- Blob naming: `{guid}/{timestamp:yyyyMMddHHmmss}-{sanitizedFileName}`

### 1e. DI Registration

In `DependencyInjection.cs`, add:

- Bind `BlobStorageOptions` from configuration section `"BlobStorage"`
- Register `IFileStorageService` → `AzureBlobStorageService` as singleton

### 1f. Docker Compose — Azurite

Add Azurite service to `docker-compose.yml`:

```yaml
azurite:
  image: mcr.microsoft.com/azure-storage/azurite:latest
  ports:
    - "10000:10000" # Blob
    - "10001:10001" # Queue
    - "10002:10002" # Table
  volumes:
    - azurite-data:/data
```

Add `BlobStorage:ConnectionString` to `appsettings.Development.json`:

```json
"BlobStorage": {
  "ConnectionString": "UseDevelopmentStorage=true"
}
```

### 1g. Unit Tests

In `tests/Chronith.Tests.Unit/Infrastructure/Storage/`:

- Test `AzureBlobStorageService` upload returns correct URL format
- Test `AzureBlobStorageService` sanitizes file names
- Test options validation (empty connection string)

**Commit:** `feat(infra): add Azure Blob Storage infrastructure for file uploads`

---

## Task 2: Domain Model — Booking Proof Fields

**Layer:** Domain only

### 2a. New Properties on `Booking`

Add to `src/Chronith.Domain/Models/Booking.cs`:

```csharp
public string? ProofOfPaymentUrl { get; private set; }
public string? ProofOfPaymentFileName { get; private set; }
public string? PaymentNote { get; private set; }
```

### 2b. New Domain Method

Add `SubmitProofOfPayment` method:

```csharp
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

    Transition(BookingStatus.PendingVerification, changedById, changedByRole);
}
```

### 2c. Unit Tests

In `tests/Chronith.Tests.Unit/Domain/BookingTests.cs` (or new file `BookingProofTests.cs`):

1. `SubmitProofOfPayment_FromPendingPayment_TransitionsToPendingVerification`
2. `SubmitProofOfPayment_SetsProofFields`
3. `SubmitProofOfPayment_AddsStatusChange`
4. `SubmitProofOfPayment_WithNullProof_StillTransitions` (proof is optional)
5. `SubmitProofOfPayment_FromWrongStatus_Throws` (test from Confirmed, Cancelled, PendingVerification, PaymentFailed)

**Commit:** `feat(domain): add proof-of-payment fields and SubmitProofOfPayment method to Booking`

---

## Task 3: Infrastructure — Entity/Config/Mapper/Migration

**Layer:** Infrastructure only

### 3a. Update `BookingEntity`

Add to `src/Chronith.Infrastructure/Persistence/Entities/BookingEntity.cs`:

```csharp
public string? ProofOfPaymentUrl { get; set; }
public string? ProofOfPaymentFileName { get; set; }
public string? PaymentNote { get; set; }
```

### 3b. Update `BookingConfiguration`

Add to `src/Chronith.Infrastructure/Persistence/Configurations/BookingConfiguration.cs`:

```csharp
builder.Property(b => b.ProofOfPaymentUrl).HasMaxLength(2048);
builder.Property(b => b.ProofOfPaymentFileName).HasMaxLength(500);
builder.Property(b => b.PaymentNote).HasMaxLength(500);
```

### 3c. Update `BookingEntityMapper`

In `src/Chronith.Infrastructure/Persistence/Mappers/BookingEntityMapper.cs`:

- `ToDomain()`: map the 3 new fields using `SetProperty` reflection helper
- `ToEntity()`: map the 3 new fields in the object initializer

### 3d. EF Migration

```bash
dotnet ef migrations add AddBookingProofFields \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API \
  --output-dir Migrations/PostgreSQL
```

### 3e. Verify

- Build succeeds
- Migration generates correct SQL (3 nullable columns added to bookings table)

**Commit:** `feat(infra): add booking proof-of-payment columns and EF migration`

---

## Task 4: Enriched `PublicBookingStatusDto`

**Layer:** Application

### 4a. New DTO

Create `src/Chronith.Application/DTOs/ManualPaymentOptionsDto.cs`:

```csharp
public sealed record ManualPaymentOptionsDto(
    string? QrCodeUrl,
    string? PublicNote,
    string Label
);
```

### 4b. Update `PublicBookingStatusDto`

Update `src/Chronith.Application/DTOs/PublicBookingStatusDto.cs`:

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
    string? PaymentMode,
    ManualPaymentOptionsDto? ManualPaymentOptions,
    string? ProofOfPaymentUrl,
    string? ProofOfPaymentFileName,
    string? PaymentNote
);
```

### 4c. Update Query Handlers

Both `GetPublicBookingStatusQueryHandler` and `GetVerifiedBookingQueryHandler` need to:

1. Inject `IBookingTypeRepository` and `ITenantPaymentConfigRepository`
2. Load the booking's `BookingType` to get `PaymentMode`
3. If `PaymentMode == Manual`, load active `TenantPaymentConfig` for provider `"Manual"` to get QR and note
4. Build `ManualPaymentOptionsDto` (null when not Manual)
5. Map proof fields from the booking domain model

Note: `IBookingRepository.GetPublicByIdAsync` currently returns a `Booking` domain model. The handler needs the `BookingType` separately — use `IBookingTypeRepository.GetByIdAsync(booking.BookingTypeId)`.

### 4d. Unit Tests

1. Handler returns `PaymentMode = "Manual"` when booking type is Manual
2. Handler returns `ManualPaymentOptions` with QR code and note
3. Handler returns null `ManualPaymentOptions` when payment mode is Automatic
4. Handler maps proof fields correctly
5. Handler still hides `CheckoutUrl` when status is not PendingPayment

**Commit:** `feat(app): enrich PublicBookingStatusDto with manual payment context`

---

## Task 5: `ConfirmManualPaymentCommand` (Customer Endpoint)

**Layer:** Application (command) + API (endpoint)

### 5a. Command

Create `src/Chronith.Application/Commands/Public/ConfirmManualPaymentCommand.cs`:

```csharp
public sealed record ConfirmManualPaymentCommand : IRequest<PublicBookingStatusDto>
{
    public required string TenantSlug { get; init; }
    public required Guid BookingId { get; init; }
    public required long Expires { get; init; }
    public required string Signature { get; init; }
    public Stream? ProofFile { get; init; }
    public string? ProofFileName { get; init; }
    public string? ProofContentType { get; init; }
    public string? PaymentNote { get; init; }
}
```

Validator:

- `TenantSlug` not empty
- `BookingId` not empty
- `Expires` > 0
- `Signature` not empty
- `PaymentNote` max 500 chars
- `ProofContentType` must be `image/jpeg`, `image/png`, or `image/webp` (when ProofFile is not null)

Handler:

1. Validate HMAC signature via `IBookingUrlSigner.Validate(bookingId, tenantSlug, expires, signature)`
2. If invalid, throw `UnauthorizedException`
3. Resolve tenant by slug
4. Get booking (public, cross-tenant via tenant ID)
5. If proof file provided, upload via `IFileStorageService.UploadAsync("payment-proofs-{tenantSlug}", ...)`
6. Call `booking.SubmitProofOfPayment(proofUrl, proofFileName, paymentNote, "customer", "customer")`
7. Save via `IBookingRepository.UpdateAsync` + `IUnitOfWork.SaveChangesAsync`
8. Publish `BookingStatusChangedNotification`
9. Return enriched `PublicBookingStatusDto` (same logic as Task 4 query handlers)

### 5b. FastEndpoints Endpoint

Create `src/Chronith.API/Endpoints/Public/ConfirmManualPaymentEndpoint.cs`:

- Route: `POST /v1/public/{tenantSlug}/bookings/{bookingId}/confirm-payment`
- Allow anonymous (HMAC auth, not JWT)
- Accepts multipart form data
- Extracts `expires` and `sig` from query parameters
- Extracts `proofFile` (IFormFile) and `paymentNote` from form data
- File size limit: 5 MB
- Maps to `ConfirmManualPaymentCommand` and sends via MediatR

### 5c. Unit Tests

1. Handler validates HMAC and throws on invalid signature
2. Handler uploads proof file when provided
3. Handler transitions booking to PendingVerification
4. Handler works without proof file (proof is optional)
5. Handler throws on invalid booking status
6. Handler publishes notification
7. Validator rejects invalid content types
8. Validator rejects notes over 500 chars

**Commit:** `feat(api): add customer confirm-manual-payment endpoint with proof upload`

---

## Task 6: Staff Verification HMAC Link

**Layer:** Application (command + interface) + Infrastructure (signer) + API (endpoint)

### 6a. Extend `IBookingUrlSigner`

Add to `src/Chronith.Application/Interfaces/IBookingUrlSigner.cs`:

```csharp
string GenerateStaffVerifyUrl(string baseUrl, Guid bookingId, string tenantSlug);
bool ValidateStaffVerify(Guid bookingId, string tenantSlug, long expires, string signature);
```

### 6b. Extend `HmacBookingUrlSigner`

In `src/Chronith.Infrastructure/Security/HmacBookingUrlSigner.cs`:

- Add `private const string StaffVerifyPrefix = "staff-verify";`
- Add `StaffVerifyLifetimeSeconds` property on `PaymentPageOptions` (default: 86400 = 24 hours)
- Implement `GenerateStaffVerifyUrl` using `StaffVerifyPrefix` + `StaffVerifyLifetimeSeconds`
- Implement `ValidateStaffVerify` using `StaffVerifyPrefix`

### 6c. Command

Create `src/Chronith.Application/Commands/Public/VerifyBookingPaymentCommand.cs`:

```csharp
public sealed record VerifyBookingPaymentCommand : IRequest<PublicBookingStatusDto>
{
    public required string TenantSlug { get; init; }
    public required Guid BookingId { get; init; }
    public required long Expires { get; init; }
    public required string Signature { get; init; }
    public required string Action { get; init; } // "approve" or "reject"
    public string? Note { get; init; }
}
```

Validator:

- `Action` must be `"approve"` or `"reject"`
- `Note` max 500 chars

Handler:

1. Validate HMAC via `IBookingUrlSigner.ValidateStaffVerify`
2. If `action == "approve"`: call `booking.Confirm("staff", "staff")`
3. If `action == "reject"`: call `booking.Cancel("staff", "staff")`
4. Save + publish notification
5. Return enriched `PublicBookingStatusDto`

### 6d. FastEndpoints Endpoint

Create `src/Chronith.API/Endpoints/Public/StaffVerifyPaymentEndpoint.cs`:

- Route: `POST /v1/public/{tenantSlug}/bookings/{bookingId}/staff-verify`
- Allow anonymous (HMAC auth)
- Extracts `expires` and `sig` from query parameters
- JSON body: `{ action, note }`

### 6e. Unit Tests

1. GenerateStaffVerifyUrl produces URL with staff-verify domain prefix
2. ValidateStaffVerify rejects customer-domain signatures (domain separation)
3. Handler approves booking (PendingVerification → Confirmed)
4. Handler rejects booking (PendingVerification → Cancelled)
5. Handler throws on invalid HMAC
6. Handler throws on wrong booking status

**Commit:** `feat(api): add staff verification HMAC endpoint for manual payments`

---

## Task 7: Notification Updates

**Layer:** Application (notification handlers)

### 7a. Include Staff Verify URL

Update the notification pipeline so that when `ToStatus == PendingVerification`, the notification payload includes a `staffVerifyUrl` field.

This requires:

1. Injecting `IBookingUrlSigner` into the relevant notification handler(s)
2. Adding `StaffVerifyBaseUrl` to `PaymentPageOptions` (default: `"https://example.com/verify"`)
3. Calling `GenerateStaffVerifyUrl` when building the notification payload
4. Adding the URL to the webhook outbox payload and email/SMS templates

### 7b. Unit Tests

1. Notification includes `staffVerifyUrl` when transitioning to PendingVerification
2. Notification does NOT include `staffVerifyUrl` for other transitions

**Commit:** `feat(app): include staff verification URL in PendingVerification notifications`

---

## Task 8: Dashboard — Payment Page

**Layer:** Dashboard (Next.js)

### 8a. New Page

Create `dashboard/src/app/(public)/book/[tenantSlug]/[btSlug]/pay/page.tsx`:

- Parse HMAC query params from URL (`bookingId`, `tenantSlug`, `expires`, `sig`)
- Fetch booking status via `GET /v1/public/{tenantSlug}/bookings/{bookingId}/status` (HMAC-signed)
- Display:
  - Booking summary (service name, date/time, amount formatted as PHP)
  - QR code image (from `ManualPaymentOptions.QrCodeUrl`) — use `<img>` tag
  - Payment instructions (from `ManualPaymentOptions.PublicNote`) — rendered as text/markdown
  - File upload area (drag-and-drop + click to browse, accepts image/jpeg, image/png, image/webp, max 5MB)
  - Payment note textarea (optional, max 500 chars)
  - "I've sent my payment" submit button
- On submit, send `POST /v1/public/{tenantSlug}/bookings/{bookingId}/confirm-payment?expires=...&sig=...` as multipart form data
- On success, redirect to success page
- Show loading state during upload
- Show error messages on failure

### 8b. Custom Hook

Create `dashboard/src/hooks/use-manual-payment.ts`:

- `useConfirmManualPayment` mutation hook (TanStack Query)
- Handles multipart form data construction

### 8c. Update Booking Confirmation Flow

In `dashboard/src/app/(public)/book/[tenantSlug]/[btSlug]/confirm/page.tsx`:

- After booking creation, if `PaymentMode == "Manual"`, redirect to `/pay` page instead of showing provider selection

**Commit:** `feat(dashboard): add manual payment page with QR display and proof upload`

---

## Task 9: Dashboard — Updated Success Page

**Layer:** Dashboard (Next.js)

### 9a. Conditional Rendering

Update `dashboard/src/app/(public)/book/[tenantSlug]/[btSlug]/success/page.tsx`:

- Fetch booking status on load
- Conditional display:
  - `Confirmed` → "Your booking is confirmed!" (green check icon)
  - `PendingVerification` → "Payment submitted! Waiting for staff verification." (clock icon)
  - `PendingPayment` → "Please complete your payment." with link to payment page (warning icon)
  - `Cancelled` → "This booking has been cancelled." (red X icon)
  - `PaymentFailed` → "Payment failed. Please try again." (red X icon)

### 9b. Proof Display

If `ProofOfPaymentUrl` is set, show a thumbnail of the uploaded proof with a "View proof" link.

**Commit:** `feat(dashboard): add conditional success page based on booking status`

---

## Task 10: Dashboard — Staff Verification Page

**Layer:** Dashboard (Next.js)

### 10a. New Page

Create `dashboard/src/app/(public)/verify/[tenantSlug]/[bookingId]/page.tsx`:

- Parse HMAC query params (`expires`, `sig`)
- Fetch booking status via `GET /v1/public/{tenantSlug}/bookings/{bookingId}/status` (using HMAC params)
- Display:
  - Customer name, email, mobile
  - Booking service name, date/time
  - Amount in PHP
  - Proof of payment image (if uploaded) — full-size, clickable to expand
  - Payment note (if provided)
  - "Approve" button (green) — calls staff-verify with `action: "approve"`
  - "Reject" button (red) — shows note input, then calls staff-verify with `action: "reject"` + note

### 10b. State Handling

- After approve/reject, show success message with final status
- If booking is already Confirmed or Cancelled, show read-only status (no action buttons)
- If HMAC is expired, show "This link has expired" message

### 10c. Custom Hook

Create or extend `dashboard/src/hooks/use-staff-verify.ts`:

- `useStaffVerify` mutation hook

**Commit:** `feat(dashboard): add staff verification page with approve/reject`

---

## Task 11: Integration & Functional Tests

**Layer:** Tests

### 11a. Functional Tests

Create `tests/Chronith.Tests.Functional/Endpoints/Public/ManualPaymentEndpointsTests.cs`:

End-to-end flow:

1. Seed tenant with Manual payment config (QrCodeUrl, PublicNote)
2. Seed a Manual-mode booking type
3. Create a booking via public endpoint → assert `PendingPayment` + `paymentUrl` returned
4. Fetch public booking status → assert `PaymentMode == "Manual"`, `ManualPaymentOptions` populated
5. Confirm manual payment with proof file → assert `PendingVerification`
6. Staff verify (approve) → assert `Confirmed`

Additional test cases:

- Customer confirm without proof file → still transitions to PendingVerification
- Staff reject → booking transitions to Cancelled
- Expired HMAC → 401
- Invalid HMAC → 401
- Wrong booking status → 400/409

### 11b. Auth Tests

Create `tests/Chronith.Tests.Functional/Endpoints/Public/ManualPaymentAuthTests.cs`:

- Both endpoints work with valid HMAC (anonymous, no JWT)
- Both endpoints reject expired signatures
- Staff verify rejects customer-domain signatures (domain separation)

### 11c. Integration Tests

If Azure Blob Storage testing is needed, add Azurite Testcontainer to integration test fixture and test upload/download cycle.

**Commit:** `test: add functional and integration tests for manual payment flow`

---

## Dependency Graph

```
Task 1 (Blob Storage)     ─┐
Task 2 (Domain)            ─┤
Task 3 (Infrastructure)    ─┤─── Task 4 (DTO) ─── Task 5 (Customer Endpoint) ─── Task 8 (Payment Page)
                            │                  └── Task 6 (Staff Verify)     ─── Task 10 (Staff Page)
                            │                  └── Task 7 (Notifications)
                            └──────────────────────────────────────────────── Task 9 (Success Page)
                                                                              Task 11 (Tests) [last]
```

Tasks 1, 2 are independent of each other.
Task 3 depends on Task 2.
Task 4 depends on Task 3.
Tasks 5, 6, 7 depend on Task 4 but are independent of each other.
Task 8 depends on Task 5.
Task 9 depends on Task 4.
Task 10 depends on Task 6.
Task 11 depends on all others.
