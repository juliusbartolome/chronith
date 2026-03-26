# Default Payment Result Pages — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create default success and failed pages in the dashboard that handle payment gateway redirects (PayMongo, Maya) with HMAC validation, so tenants have working payment result pages out of the box.

**Architecture:** Two new client-component pages at `/payment/success` and `/payment/failed` in the `(public)` route group. Both read `bookingId`, `tenantSlug`, `expires`, `sig` from URL query params and validate via the existing `PublicVerifyBookingEndpoint` (`GET /v1/public/{tenantSlug}/bookings/{bookingId}/verify`). A shared `usePaymentResult` hook handles the verification API call. The success page polls until booking status changes from `PendingPayment` (to handle the webhook race condition). No API-side changes needed.

**Tech Stack:** Next.js 15, React 19, Tailwind CSS, shadcn/ui (Button, Card)

---

## Existing Infrastructure (no changes needed)

- **HMAC signer:** `HmacBookingUrlSigner` in `src/Chronith.Infrastructure/Security/` — signs URLs with `bookingId`, `tenantSlug`, `expires`, `sig` query params.
- **Verify endpoint:** `GET /v1/public/{tenantSlug}/bookings/{bookingId}/verify?expires=...&sig=...` — validates HMAC, returns `PublicBookingStatusDto`.
- **`PublicBookingStatusDto`:** `{ Id, ReferenceId, Status, Start, End, AmountInCentavos, Currency, PaymentReference, CheckoutUrl }`.
- **URL construction in `CreatePublicCheckoutHandler`:**
  ```
  var defaultSuccessUrl = $"{baseUrl}/success";
  var defaultFailureUrl = $"{baseUrl}/failed";
  ```
  Where `baseUrl` comes from `PaymentPage:BaseUrl` (global default), tenant config, or request override.
  `GenerateSignedUrl(resolvedSuccessBase, bookingId, tenantSlug)` appends query params.

## Key Design Decisions

- **HMAC validation required** before showing booking details (prevents guessing booking IDs).
- **Success page polls** because the redirect can arrive before the webhook processes (race condition). Polls every 3s, max ~30s.
- **Failed page does NOT poll** — just shows current state.
- **Generic Chronith branding** — no tenant branding fetch (keeps it simple).
- **"Book Again" link** on failure → `/book/{tenantSlug}` (tenant slug is in the query params).
- **No "retry payment" button** — `PaymentFailed` is terminal. Customer books again.

---

### Task 1: Dashboard API proxy route for payment verification

**Files:**

- Create: `dashboard/src/app/api/public/payment/verify/route.ts`

**What to build:**
A Next.js API route that proxies the verify request to the Chronith API backend.

- Route: `GET /api/public/payment/verify?bookingId=...&tenantSlug=...&expires=...&sig=...`
- Extract `bookingId`, `tenantSlug`, `expires`, `sig` from request URL search params.
- Construct backend URL: `GET /v1/public/{tenantSlug}/bookings/{bookingId}/verify?expires={expires}&sig={sig}`
- Forward to backend using the same pattern as other proxy routes in the project.
- Return the response (JSON body + status code).
- Use `proxyToApi` helper from `dashboard/src/lib/proxy.ts` with `unauthenticated: true`.

**Reference:** See `dashboard/src/app/api/public/[tenantSlug]/bookings/route.ts` for the proxy pattern.

**Commit:** `feat(dashboard): add payment verify API proxy route`

---

### Task 2: Shared `usePaymentResult` hook

**Files:**

- Create: `dashboard/src/hooks/use-payment-result.ts`

**What to build:**
A React hook that handles the full lifecycle of verifying a payment result URL.

**Interface:**

```typescript
interface PaymentResultParams {
  bookingId: string | null;
  tenantSlug: string | null;
  expires: string | null;
  sig: string | null;
}

interface BookingStatus {
  id: string;
  referenceId: string;
  status:
    | "PendingPayment"
    | "PendingVerification"
    | "Confirmed"
    | "Cancelled"
    | "PaymentFailed";
  start: string;
  end: string;
  amountInCentavos: number;
  currency: string;
  paymentReference: string | null;
  checkoutUrl: string | null;
}

type VerificationState =
  | { status: "loading" }
  | { status: "verified"; booking: BookingStatus }
  | { status: "invalid" } // HMAC invalid or HTTP 401
  | { status: "error"; message: string }; // network/server error

function usePaymentResult(
  params: PaymentResultParams,
  options?: { poll?: boolean },
): VerificationState;
```

**Behavior:**

1. On mount, if any param is missing → return `{ status: "invalid" }`.
2. Call `GET /api/public/payment/verify?bookingId=...&tenantSlug=...&expires=...&sig=...`.
3. On 401 → `{ status: "invalid" }`.
4. On 2xx → `{ status: "verified", booking: data }`.
5. On other error → `{ status: "error", message: "..." }`.
6. If `options.poll === true` and booking status is `PendingPayment`, re-fetch every 3 seconds for up to 10 attempts. Stop polling once status is not `PendingPayment`.

**Commit:** `feat(dashboard): add usePaymentResult hook`

---

### Task 3: Payment success page

**Files:**

- Create: `dashboard/src/app/(public)/payment/success/page.tsx`

**What to build:**
A client component page at `/payment/success` that shows the payment result after a customer returns from the payment gateway.

**URL:** `/payment/success?bookingId=...&tenantSlug=...&expires=...&sig=...`

**Uses:** `usePaymentResult(params, { poll: true })`

**Visual states:**

1. **Loading** (`status === "loading"`):
   - Centered spinner + "Verifying payment..."

2. **Verified + Confirmed** (`status === "verified"` && `booking.status === "Confirmed"`):
   - Green circle with checkmark icon (same style as existing success page)
   - Heading: "Payment Successful!"
   - Subheading: "Your booking has been confirmed."
   - Card with booking details:
     - Reference ID: `booking.referenceId` (first 8 chars, mono font)
     - Date: formatted from `booking.start`
     - Time: formatted from `booking.start` - `booking.end`
     - Amount: formatted as PHP with centavos (e.g., "₱1,500.00")
   - "Book Another Appointment" button → `/book/{tenantSlug}`

3. **Verified + PendingPayment** (`booking.status === "PendingPayment"`, polling active):
   - Spinner + "Confirming your booking..."
   - Subtext: "This may take a few moments."

4. **Verified + PaymentFailed/Cancelled** (`booking.status` is `PaymentFailed` or `Cancelled`):
   - Red circle with X icon
   - "Something went wrong"
   - "Your payment could not be processed."
   - "Book Again" button → `/book/{tenantSlug}`

5. **Invalid HMAC** (`status === "invalid"`):
   - Warning icon
   - "This link has expired or is invalid"
   - No booking details shown

6. **Error** (`status === "error"`):
   - "Something went wrong. Please try again later."

**Style:** Follow the existing success page pattern (`max-w-lg mx-auto px-4 py-16 text-center`). Use shadcn/ui `Button` and `Card`/`CardContent` components.

**Formatting helpers:**

- Amount: `(amountInCentavos / 100).toLocaleString("en-PH", { style: "currency", currency: "PHP" })`
- Date: `new Date(booking.start).toLocaleDateString("en-PH", { weekday: "long", year: "numeric", month: "long", day: "numeric" })`
- Time: `new Date(booking.start).toLocaleTimeString("en-PH", { hour: "numeric", minute: "2-digit" })` + " – " + same for end

**Commit:** `feat(dashboard): add payment success page`

---

### Task 4: Payment failed page

**Files:**

- Create: `dashboard/src/app/(public)/payment/failed/page.tsx`

**What to build:**
A client component page at `/payment/failed` that shows when a customer cancels or fails payment at the gateway.

**URL:** `/payment/failed?bookingId=...&tenantSlug=...&expires=...&sig=...`

**Uses:** `usePaymentResult(params)` (NO polling)

**Visual states:**

1. **Loading** (`status === "loading"`):
   - Centered spinner + "Loading..."

2. **Verified** (`status === "verified"`, any booking status):
   - Red circle with X icon
   - Heading: "Payment Not Completed"
   - Subheading: "Your payment was cancelled or could not be processed."
   - If booking reference exists: show "Reference: {referenceId first 8 chars}"
   - "Book Again" button → `/book/{tenantSlug}`

3. **Invalid HMAC** (`status === "invalid"`):
   - Warning icon
   - "This link has expired or is invalid"

4. **Error** (`status === "error"`):
   - "Something went wrong. Please try again later."

**Style:** Same centered layout as success page. Use shadcn/ui `Button`.

**Commit:** `feat(dashboard): add payment failed page`

---

### Task 5: Update appsettings.json BaseUrl comment

**Files:**

- Modify: `src/Chronith.API/appsettings.json`

**What to do:**
Update the `PaymentPage:BaseUrl` value to use a more descriptive placeholder that indicates it should point to the dashboard's `/payment` path:

```json
"PaymentPage": {
    "BaseUrl": "https://your-dashboard-host/payment",
    "TokenLifetimeSeconds": 3600
}
```

Also update the PayMongo/Maya fallback success/failure URLs to match:

```json
"SuccessUrl": "https://your-dashboard-host/payment/success",
"FailureUrl": "https://your-dashboard-host/payment/failed"
```

**Commit:** `docs: update payment URL placeholders in appsettings`

---

### Task 6: Build verification

**Steps:**

1. Run `dotnet build Chronith.slnx` — expect 0 warnings, 0 errors.
2. Run Next.js build: `cd dashboard && npm run build` — expect success.
3. Run `dotnet test tests/Chronith.Tests.Unit` — expect all pass (no API changes, so existing tests should be unaffected).

No commit for this task — just verification.
