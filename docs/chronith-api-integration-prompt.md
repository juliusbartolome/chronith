# Chronith Booking Engine — API Integration Reference (AI Prompt Context)

> **Purpose:** This document is a comprehensive AI prompt context block for Claude Opus 4.6 (or any LLM). Feed it as system context so the model can accurately answer integration questions, generate client code, and troubleshoot issues for developers building on the Chronith booking engine API.

---

## 1. System Overview

**Chronith** is a multi-tenant booking engine REST API. It handles the full booking lifecycle — creating booking types, managing availability, processing payments, sending notifications, and tracking analytics — for any number of isolated tenants.

### Technical Stack

| Concern       | Technology                                                             |
| ------------- | ---------------------------------------------------------------------- |
| Runtime       | .NET 10                                                                |
| Web framework | FastEndpoints 8.x                                                      |
| Database      | PostgreSQL 17                                                          |
| Cache         | Redis 8 (StackExchange.Redis)                                          |
| Auth          | JWT (HMAC symmetric) + API Key                                         |
| Payments      | Pluggable (`IPaymentProvider`): Stub, PayMongo                         |
| Notifications | Email (MailKit/SMTP), SMS (Twilio), Push (Firebase)                    |
| Currency      | PHP only, stored as `long` in centavos (e.g., `250000` = PHP 2,500.00) |

### API Conventions

- **Base URL:** `https://{host}/` — no version prefix
- **Content-Type:** `application/json` for all requests and responses
- **Naming:** JSON properties use `camelCase`
- **Enums:** Serialized as strings (e.g., `"PendingPayment"`, `"TimeSlot"`)
- **Dates:** ISO 8601 `DateTimeOffset` (e.g., `"2026-03-15T09:00:00+08:00"`)
- **Times:** `TimeOnly` as `"HH:mm:ss"` (e.g., `"09:00:00"`)
- **IDs:** UUID/GUID strings (e.g., `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"`)

### Tenant Isolation

Every resource belongs to exactly one tenant. Isolation is enforced at the database level via EF Core global query filters on `TenantId`. Authenticated endpoints derive the tenant from the JWT `tenant_id` claim or from the API key binding. Public endpoints use the `{tenantSlug}` path parameter.

---

## 2. Authentication & Authorization

Chronith supports three auth mechanisms: **JWT tokens** (for admin/staff dashboards), **API keys** (for server-to-server), and **Customer auth** (for public booking flows).

### 2.1 JWT Authentication (Admin/Staff)

#### Login

```
POST /auth/login
```

**Request:**

```json
{
  "tenantSlug": "acme-studio",
  "email": "admin@acme.com",
  "password": "s3cur3P@ss"
}
```

**Response (200):**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2g...",
  "tokenType": "Bearer"
}
```

- Access tokens expire in **15 minutes**.
- Include in subsequent requests: `Authorization: Bearer {accessToken}`

#### Refresh

```
POST /auth/refresh
```

**Request:**

```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2g..."
}
```

**Response (200):** Same shape as login — new access + refresh token pair. Old refresh token is invalidated (rotation).

#### Register (Tenant User)

```
POST /auth/register
```

**Request:**

```json
{
  "tenantSlug": "acme-studio",
  "email": "staff@acme.com",
  "password": "s3cur3P@ss",
  "name": "Jane Doe"
}
```

#### Current User

```
GET /auth/me
```

**Response (200):**

```json
{
  "id": "a1b2c3d4-...",
  "tenantId": "t1e2n3a4-...",
  "email": "admin@acme.com",
  "name": "Admin User",
  "role": "Owner"
}
```

#### JWT Claims

| Claim       | Description                        |
| ----------- | ---------------------------------- |
| `sub`       | User ID (GUID)                     |
| `tenant_id` | Tenant ID (GUID)                   |
| `role`      | One of: `Owner`, `Admin`, `Member` |
| `email`     | User email                         |

### 2.2 API Key Authentication (Server-to-Server)

Send the key in the `X-Api-Key` header. Each key is bound to a specific tenant.

```
X-Api-Key: ck_live_abc123def456...
```

#### Create API Key

```
POST /api-keys
```

**Request:**

```json
{
  "description": "Production backend",
  "role": "Admin"
}
```

**Response (201):** Returns the key value **once** — it is hashed and cannot be retrieved again.

```json
{
  "id": "k1e2y3...",
  "description": "Production backend",
  "role": "Admin",
  "isRevoked": false,
  "createdAt": "2026-03-15T00:00:00+08:00",
  "lastUsedAt": null,
  "key": "ck_live_abc123def456..."
}
```

#### List API Keys

```
GET /api-keys
```

**Response (200):**

```json
[
  {
    "id": "k1e2y3...",
    "description": "Production backend",
    "role": "Admin",
    "isRevoked": false,
    "createdAt": "2026-03-15T00:00:00+08:00",
    "lastUsedAt": "2026-03-15T12:30:00+08:00"
  }
]
```

#### Revoke API Key

```
DELETE /api-keys/{id}
```

**Response:** `204 No Content`

### 2.3 Customer Authentication (Public Booking Flow)

For end-customers booking through the public interface. All customer auth endpoints are under `/public/{tenantSlug}/auth/`.

#### Register (Built-in)

```
POST /public/{tenantSlug}/auth/register
```

**Request:**

```json
{
  "email": "customer@example.com",
  "password": "cust0m3rP@ss",
  "name": "Maria Santos",
  "phone": "+639171234567"
}
```

**Response (201):**

```json
{
  "accessToken": "eyJ...",
  "refreshToken": "ref...",
  "customer": {
    "id": "c1u2s3t4-...",
    "email": "customer@example.com",
    "name": "Maria Santos",
    "phone": "+639171234567",
    "authProvider": "BuiltIn",
    "isEmailVerified": false,
    "createdAt": "2026-03-15T10:00:00+08:00"
  }
}
```

#### Login

```
POST /public/{tenantSlug}/auth/login
```

**Request:**

```json
{
  "email": "customer@example.com",
  "password": "cust0m3rP@ss"
}
```

#### Magic Link (Passwordless)

**Request magic link:**

```
POST /public/{tenantSlug}/auth/magic-link/register
```

```json
{
  "email": "customer@example.com",
  "name": "Maria Santos"
}
```

**Verify magic link token:**

```
POST /public/{tenantSlug}/auth/magic-link/verify
```

```json
{
  "token": "ml_abc123..."
}
```

#### Refresh Customer Token

```
POST /public/{tenantSlug}/auth/refresh
```

#### Customer Profile

```
GET /public/{tenantSlug}/auth/me
```

#### Customer Bookings

```
GET /public/{tenantSlug}/auth/my-bookings
GET /public/{tenantSlug}/auth/my-bookings/{id}
```

---

## 3. Multi-Tenancy

### Tenant Self-Signup

```
POST /signup
```

**Request:**

```json
{
  "name": "Acme Photography Studio",
  "slug": "acme-studio",
  "email": "owner@acme.com",
  "password": "0wn3rP@ss"
}
```

Creates both the tenant and the owner user account. Returns a verification token (sent via email).

#### Email Verification

```
POST /signup/verify-email
```

```json
{
  "token": "verify_abc123..."
}
```

### Tenant Settings (Branding)

```
GET /tenant/settings
```

**Response (200):**

```json
{
  "id": "s1e2t3...",
  "tenantId": "t1e2n3...",
  "logoUrl": "https://cdn.acme.com/logo.png",
  "primaryColor": "#2563EB",
  "accentColor": "#F59E0B",
  "customDomain": "book.acme.com",
  "bookingPageEnabled": true,
  "welcomeMessage": "Welcome to Acme Studio! Book your session below.",
  "termsUrl": "https://acme.com/terms",
  "privacyUrl": "https://acme.com/privacy",
  "updatedAt": "2026-03-15T00:00:00+08:00"
}
```

```
PUT /tenant/settings
```

**Request:** Same shape as response (minus `id`, `tenantId`, `updatedAt`).

### Tenant Auth Config

Configure how customers authenticate for this tenant:

```
GET /tenant/auth-config
PUT /tenant/auth-config
```

Supports: `BuiltIn` (email+password), `MagicLink` (passwordless), `Oidc` (external IdP).

---

## 4. Core API Endpoints (Authenticated)

All endpoints in this section require either a valid JWT `Authorization: Bearer {token}` header or an `X-Api-Key` header. Responses are scoped to the authenticated tenant.

### 4.1 Booking Types

Booking types define what can be booked. Two kinds exist: **TimeSlot** (fixed-duration appointments) and **Calendar** (full-day or flexible bookings).

#### Create Booking Type

```
POST /booking-types
```

**Request (TimeSlot example):**

```json
{
  "slug": "photography-session",
  "name": "Photography Session",
  "isTimeSlot": true,
  "capacity": 1,
  "paymentMode": "Automatic",
  "paymentProvider": "PayMongo",
  "priceInCentavos": 250000,
  "currency": "PHP",
  "requiresStaffAssignment": true,
  "durationMinutes": 60,
  "bufferBeforeMinutes": 15,
  "bufferAfterMinutes": 15,
  "availabilityWindows": [
    { "dayOfWeek": "Monday", "startTime": "09:00:00", "endTime": "17:00:00" },
    { "dayOfWeek": "Tuesday", "startTime": "09:00:00", "endTime": "17:00:00" },
    {
      "dayOfWeek": "Wednesday",
      "startTime": "09:00:00",
      "endTime": "17:00:00"
    },
    { "dayOfWeek": "Thursday", "startTime": "09:00:00", "endTime": "17:00:00" },
    { "dayOfWeek": "Friday", "startTime": "09:00:00", "endTime": "17:00:00" }
  ]
}
```

**Request (Calendar example):**

```json
{
  "slug": "venue-rental",
  "name": "Venue Rental",
  "isTimeSlot": false,
  "capacity": 1,
  "paymentMode": "Manual",
  "priceInCentavos": 1500000,
  "currency": "PHP",
  "requiresStaffAssignment": false,
  "availableDays": [
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
    "Saturday"
  ]
}
```

**Response (201):**

```json
{
  "id": "bt-uuid-...",
  "slug": "photography-session",
  "name": "Photography Session",
  "kind": "TimeSlot",
  "capacity": 1,
  "paymentMode": "Automatic",
  "paymentProvider": "PayMongo",
  "priceInCentavos": 250000,
  "currency": "PHP",
  "durationMinutes": 60,
  "bufferBeforeMinutes": 15,
  "bufferAfterMinutes": 15,
  "availabilityWindows": [
    { "dayOfWeek": "Monday", "startTime": "09:00:00", "endTime": "17:00:00" }
  ],
  "availableDays": null,
  "requiresStaffAssignment": true,
  "customFieldSchema": null,
  "reminderIntervals": null,
  "customerCallbackUrl": null
}
```

#### List Booking Types

```
GET /booking-types
```

**Response (200):** Array of `BookingTypeDto`.

#### Get Booking Type

```
GET /booking-types/{slug}
```

#### Update Booking Type

```
PUT /booking-types/{slug}
```

Same body shape as create.

#### Delete Booking Type (Soft Delete)

```
DELETE /booking-types/{slug}
```

**Response:** `204 No Content`

### 4.2 Bookings

#### Create Booking

```
POST /bookings
```

**Request:**

```json
{
  "bookingTypeSlug": "photography-session",
  "startTime": "2026-03-20T10:00:00+08:00",
  "customerEmail": "customer@example.com",
  "customerId": "c1u2s3t4-..."
}
```

**Response (201):**

```json
{
  "id": "b1o2o3k4-...",
  "bookingTypeId": "bt-uuid-...",
  "start": "2026-03-20T10:00:00+08:00",
  "end": "2026-03-20T11:00:00+08:00",
  "status": "PendingPayment",
  "customerId": "c1u2s3t4-...",
  "customerEmail": "customer@example.com",
  "paymentReference": null,
  "amountInCentavos": 250000,
  "currency": "PHP",
  "checkoutUrl": "https://checkout.paymongo.com/cs_...",
  "staffMemberId": null,
  "statusChanges": []
}
```

> **Note:** If `priceInCentavos` is `0` (free booking), `status` will be `"PendingVerification"` instead of `"PendingPayment"`. If `paymentMode` is `"Automatic"`, a `checkoutUrl` is returned for redirect.

#### List Bookings

```
GET /bookings?status=Confirmed&bookingTypeSlug=photography-session&from=2026-03-01&to=2026-03-31&page=1&pageSize=20
```

**Response (200):**

```json
{
  "items": [
    /* BookingDto[] */
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20
}
```

#### Get Booking

```
GET /bookings/{id}
```

#### Cancel Booking

```
DELETE /bookings/{id}
```

Transitions status to `Cancelled`. **Response:** `204 No Content`

#### Confirm Booking

```
POST /bookings/{id}/confirm
```

Transitions from `PendingVerification` to `Confirmed`.

**Response (200):** Updated `BookingDto`.

#### Pay Booking (Manual Payment)

```
POST /bookings/{id}/pay
```

**Request:**

```json
{
  "bookingId": "b1o2o3k4-...",
  "paymentReference": "REF-12345"
}
```

Transitions from `PendingPayment` to `PendingVerification`.

**Response (200):** Updated `BookingDto`.

#### Reschedule Booking

```
POST /bookings/{id}/reschedule
```

**Request:**

```json
{
  "bookingId": "b1o2o3k4-...",
  "newStartTime": "2026-03-21T14:00:00+08:00"
}
```

#### Assign Staff to Booking

```
POST /bookings/{id}/assign-staff
```

**Request:**

```json
{
  "bookingId": "b1o2o3k4-...",
  "staffMemberId": "s1t2a3f4-..."
}
```

#### Export Bookings

```
GET /bookings/export?format=csv&from=2026-03-01&to=2026-03-31
```

Formats: `csv`, `excel`. Returns file download.

### 4.3 Availability

#### Get Available Slots

```
GET /booking-types/{slug}/availability?date=2026-03-20
```

**Response (200):**

```json
{
  "slots": [
    {
      "start": "2026-03-20T09:00:00+08:00",
      "end": "2026-03-20T10:00:00+08:00"
    },
    {
      "start": "2026-03-20T10:00:00+08:00",
      "end": "2026-03-20T11:00:00+08:00"
    },
    {
      "start": "2026-03-20T11:00:00+08:00",
      "end": "2026-03-20T12:00:00+08:00"
    },
    { "start": "2026-03-20T13:00:00+08:00", "end": "2026-03-20T14:00:00+08:00" }
  ]
}
```

Slots already booked at capacity are excluded. Buffer times are accounted for internally.

### 4.4 Staff Management

#### Create Staff

```
POST /staff
```

**Request:**

```json
{
  "name": "Juan Dela Cruz",
  "email": "juan@acme.com",
  "tenantUserId": null,
  "availabilityWindows": [
    { "dayOfWeek": "Monday", "startTime": "09:00:00", "endTime": "17:00:00" },
    {
      "dayOfWeek": "Wednesday",
      "startTime": "09:00:00",
      "endTime": "17:00:00"
    },
    { "dayOfWeek": "Friday", "startTime": "09:00:00", "endTime": "17:00:00" }
  ]
}
```

**Response (201):**

```json
{
  "id": "s1t2a3f4-...",
  "name": "Juan Dela Cruz",
  "email": "juan@acme.com",
  "tenantUserId": null,
  "isActive": true,
  "availabilityWindows": [
    { "dayOfWeek": "Monday", "startTime": "09:00:00", "endTime": "17:00:00" }
  ]
}
```

#### List Staff

```
GET /staff
```

#### Get Staff

```
GET /staff/{id}
```

#### Update Staff

```
PUT /staff/{id}
```

#### Delete Staff (Deactivate)

```
DELETE /staff/{id}
```

#### Get Staff Availability

```
GET /staff/{id}/availability?date=2026-03-20
```

**Response (200):** Same `AvailabilityDto` shape — available time slots for that staff member on the given date.

### 4.5 Recurring Bookings

#### Create Recurrence Rule

```
POST /recurring-bookings
```

**Request:**

```json
{
  "bookingTypeSlug": "photography-session",
  "customerId": "c1u2s3t4-...",
  "staffMemberId": "s1t2a3f4-...",
  "frequency": "Weekly",
  "interval": 1,
  "daysOfWeek": ["Monday", "Wednesday"],
  "startTime": "10:00:00",
  "duration": "01:00:00",
  "seriesStart": "2026-04-01",
  "seriesEnd": "2026-06-30",
  "maxOccurrences": null
}
```

**Response (201):**

```json
{
  "id": "rr-uuid-...",
  "tenantId": "t1e2n3...",
  "bookingTypeId": "bt-uuid-...",
  "customerId": "c1u2s3t4-...",
  "staffMemberId": "s1t2a3f4-...",
  "frequency": "Weekly",
  "interval": 1,
  "daysOfWeek": ["Monday", "Wednesday"],
  "startTime": "10:00:00",
  "duration": "01:00:00",
  "seriesStart": "2026-04-01",
  "seriesEnd": "2026-06-30",
  "maxOccurrences": null,
  "isActive": true,
  "createdAt": "2026-03-15T10:00:00+08:00"
}
```

#### Other Recurrence Endpoints

```
GET    /recurring-bookings                    — List rules
GET    /recurring-bookings/{id}               — Get rule detail
PUT    /recurring-bookings/{id}               — Update rule
DELETE /recurring-bookings/{id}               — Cancel rule
GET    /recurring-bookings/{id}/bookings      — List generated booking instances
```

### 4.6 Waitlist

```
POST   /waitlist                              — Join waitlist
GET    /waitlist                              — List entries
POST   /waitlist/{id}/accept                  — Accept an offer
DELETE /waitlist/{id}                         — Remove from waitlist
```

**Waitlist Entry:**

```json
{
  "id": "wl-uuid-...",
  "bookingTypeId": "bt-uuid-...",
  "staffMemberId": null,
  "customerId": "c1u2s3t4-...",
  "customerEmail": "customer@example.com",
  "desiredStart": "2026-03-20T10:00:00+08:00",
  "desiredEnd": "2026-03-20T11:00:00+08:00",
  "status": "Waiting",
  "offeredAt": null,
  "expiresAt": null,
  "createdAt": "2026-03-15T10:00:00+08:00"
}
```

**WaitlistStatus values:** `Waiting`, `Offered`, `Expired`, `Converted`

A background service automatically promotes waitlist entries when slots open.

### 4.7 Time Blocks

Block availability for staff or booking types:

```
POST   /time-blocks
GET    /time-blocks
DELETE /time-blocks/{id}
```

**Create Request:**

```json
{
  "bookingTypeId": "bt-uuid-...",
  "staffMemberId": "s1t2a3f4-...",
  "start": "2026-03-25T09:00:00+08:00",
  "end": "2026-03-25T12:00:00+08:00",
  "reason": "Staff training"
}
```

### 4.8 Webhooks

Register URLs to receive booking lifecycle events:

```
POST   /webhooks
GET    /webhooks
DELETE /webhooks/{id}
GET    /webhooks/{id}/deliveries              — List delivery attempts
POST   /webhooks/{id}/deliveries/{deliveryId}/retry  — Retry failed delivery
```

**Create Request:**

```json
{
  "bookingTypeSlug": "photography-session",
  "url": "https://yourapp.com/webhooks/chronith",
  "secret": "whsec_a1b2c3d4e5f6g7h8"
}
```

**Response (201):**

```json
{
  "id": "wh-uuid-...",
  "url": "https://yourapp.com/webhooks/chronith"
}
```

### 4.9 Notifications

#### List Notification Configs

```
GET /notifications/config
```

**Response (200):**

```json
[
  {
    "id": "nc-uuid-...",
    "channelType": "Email",
    "isEnabled": true,
    "settings": "{\"smtpHost\":\"smtp.gmail.com\",\"smtpPort\":587,...}",
    "createdAt": "2026-03-01T00:00:00+08:00",
    "updatedAt": "2026-03-15T00:00:00+08:00"
  }
]
```

#### Update Notification Config

```
PUT /notifications/config/{channel}
```

Where `{channel}` is `Email`, `Sms`, or `Push`.

#### Disable Notification Channel

```
DELETE /notifications/config/{channel}
```

### 4.10 Notification Templates

```
GET    /notification-templates                      — List all
GET    /notification-templates/{id}                 — Get by ID
PUT    /notification-templates/{id}                 — Update
POST   /notification-templates/{id}/preview         — Preview with sample data
POST   /notification-templates/{id}/reset           — Reset to default
```

**Template shape:**

```json
{
  "id": "nt-uuid-...",
  "tenantId": "t1e2n3...",
  "eventType": "booking.confirmed",
  "channelType": "Email",
  "subject": "Your booking is confirmed!",
  "body": "Hi {{customerName}}, your {{bookingTypeName}} on {{bookingDate}} is confirmed.",
  "isActive": true,
  "createdAt": "2026-03-01T00:00:00+08:00",
  "updatedAt": "2026-03-15T00:00:00+08:00"
}
```

### 4.11 Analytics

```
GET /analytics/bookings?from=2026-03-01&to=2026-03-31
```

**Response (200):**

```json
{
  "period": "2026-03-01 to 2026-03-31",
  "total": 150,
  "byStatus": {
    "PendingPayment": 10,
    "PendingVerification": 15,
    "Confirmed": 120,
    "Cancelled": 5
  },
  "byBookingType": [
    {
      "slug": "photography-session",
      "name": "Photography Session",
      "count": 80
    },
    { "slug": "haircut", "name": "Haircut", "count": 70 }
  ],
  "byStaff": [
    { "staffId": "s1t2a3f4-...", "name": "Juan Dela Cruz", "count": 65 }
  ],
  "timeSeries": [
    { "date": "2026-03-01", "count": 5 },
    { "date": "2026-03-02", "count": 8 }
  ]
}
```

```
GET /analytics/revenue?from=2026-03-01&to=2026-03-31
```

**Response (200):**

```json
{
  "period": "2026-03-01 to 2026-03-31",
  "totalCentavos": 37500000,
  "currency": "PHP",
  "byBookingType": [
    { "slug": "photography-session", "totalCentavos": 20000000, "count": 80 }
  ],
  "timeSeries": [{ "date": "2026-03-01", "totalCentavos": 1250000, "count": 5 }]
}
```

```
GET /analytics/utilization?from=2026-03-01&to=2026-03-31
```

**Response (200):**

```json
{
  "period": "2026-03-01 to 2026-03-31",
  "byBookingType": [
    {
      "slug": "photography-session",
      "totalSlots": 200,
      "bookedSlots": 80,
      "utilizationRate": 0.4
    }
  ],
  "byStaff": [
    {
      "staffId": "s1t2a3f4-...",
      "name": "Juan Dela Cruz",
      "totalSlots": 100,
      "bookedSlots": 65,
      "utilizationRate": 0.65
    }
  ]
}
```

#### Export Analytics

```
GET /analytics/export?format=csv&from=2026-03-01&to=2026-03-31
```

### 4.12 Audit Log

```
GET /audit?from=2026-03-01&to=2026-03-31&page=1&pageSize=50
```

**Response item:**

```json
{
  "id": "au-uuid-...",
  "userId": "u1s2e3r4-...",
  "userRole": "Owner",
  "entityType": "Booking",
  "entityId": "b1o2o3k4-...",
  "action": "Create",
  "oldValues": null,
  "newValues": "{\"status\":\"PendingPayment\",\"customerEmail\":\"customer@example.com\"}",
  "metadata": null,
  "timestamp": "2026-03-15T10:00:00+08:00"
}
```

```
GET /audit/{id}
GET /audit/export?format=csv
```

### 4.13 Tenant Info

```
GET /tenant                  — Current tenant details
GET /tenant/metrics          — Tenant usage metrics
```

---

## 5. Public API Endpoints (Anonymous / Customer)

These endpoints require **no authentication** (or optional customer auth) and are used by booking widgets, public pages, and customer-facing applications. They use the `{tenantSlug}` path to identify the tenant.

### 5.1 Browse Booking Types

```
GET /public/{tenantSlug}/booking-types
```

**Response (200):** Array of `BookingTypeDto` (same shape as Section 4.1).

```
GET /public/{tenantSlug}/booking-types/{slug}
```

### 5.2 Check Availability

```
GET /public/{tenantSlug}/booking-types/{slug}/availability?date=2026-03-20
```

**Response (200):** Same `AvailabilityDto` shape as Section 4.3.

### 5.3 Create Booking (Public)

```
POST /public/{tenantSlug}/booking-types/{slug}/bookings
```

**Request:**

```json
{
  "startTime": "2026-03-20T10:00:00+08:00",
  "customerEmail": "customer@example.com",
  "customerId": "c1u2s3t4-..."
}
```

**Response (201):** `BookingDto` (same as Section 4.2).

### 5.4 Browse Staff

```
GET /public/{tenantSlug}/staff
GET /public/{tenantSlug}/staff/{id}/availability?date=2026-03-20
```

### 5.5 Join Waitlist (Public)

```
POST /public/{tenantSlug}/booking-types/{slug}/waitlist
```

**Request:**

```json
{
  "customerEmail": "customer@example.com",
  "desiredStart": "2026-03-20T10:00:00+08:00",
  "desiredEnd": "2026-03-20T11:00:00+08:00"
}
```

### 5.6 iCal Feed

```
GET /booking-types/{slug}/calendar.ics
```

Returns an iCalendar (`.ics`) file with all confirmed bookings for the given booking type. Can be subscribed to from Google Calendar, Outlook, Apple Calendar, etc.

---

## 6. Booking Status Machine

### Statuses

| Status               | Value                   | Description                                   |
| -------------------- | ----------------------- | --------------------------------------------- |
| Pending Payment      | `"PendingPayment"`      | Awaiting payment (only for paid bookings)     |
| Pending Verification | `"PendingVerification"` | Payment received, awaiting admin confirmation |
| Confirmed            | `"Confirmed"`           | Booking is confirmed and active               |
| Cancelled            | `"Cancelled"`           | Booking has been cancelled (terminal)         |

### Transitions

```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│   [Booking Created]                                         │
│         │                                                   │
│         ├── price > 0 ──► PendingPayment                    │
│         │                      │                            │
│         │                      │ POST /bookings/{id}/pay    │
│         │                      ▼                            │
│         └── price = 0 ──► PendingVerification               │
│                                │                            │
│                                │ POST /bookings/{id}/confirm│
│                                ▼                            │
│                           Confirmed                         │
│                                                             │
│   Any non-Cancelled state ──► Cancelled (terminal)          │
│         via DELETE /bookings/{id}                           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Key Rules

1. **Free bookings** (`priceInCentavos = 0`) skip `PendingPayment` entirely and start at `PendingVerification`.
2. **Automatic payment mode**: On booking creation, a checkout URL is generated and returned in `checkoutUrl`. The customer is redirected to pay. When the payment provider calls back via `POST /webhooks/payments/{provider}`, the status moves to `PendingVerification`.
3. **Manual payment mode**: An admin/staff calls `POST /bookings/{id}/pay` with an optional `paymentReference` string.
4. **Cancellation** is allowed from any non-cancelled state and is irreversible.
5. Each transition is tracked in `statusChanges` array on the booking, recording who changed it, their role, and when.

---

## 7. Payment Integration

### Currency

All monetary values are in **PHP (Philippine Peso)** stored as **centavos** (`long` integer).

| Display Amount | `amountInCentavos` value |
| -------------- | ------------------------ |
| PHP 500.00     | `50000`                  |
| PHP 2,500.00   | `250000`                 |
| PHP 15,000.00  | `1500000`                |

### Payment Modes

| Mode        | Behavior                                                                 |
| ----------- | ------------------------------------------------------------------------ |
| `Manual`    | Admin/staff manually marks payment via `POST /bookings/{id}/pay`         |
| `Automatic` | System creates checkout session via payment provider on booking creation |

### Automatic Payment Flow

1. **Booking created** → Status: `PendingPayment`, `checkoutUrl` returned
2. **Customer redirected** to `checkoutUrl` (e.g., PayMongo checkout page)
3. **Customer pays** on the provider's page
4. **Provider calls back** → `POST /webhooks/payments/{provider}` (e.g., `POST /webhooks/payments/PayMongo`)
5. **System processes** webhook → Status moves to `PendingVerification`
6. **Admin confirms** → `POST /bookings/{id}/confirm` → Status: `Confirmed`

### Manual Payment Flow

1. **Booking created** → Status: `PendingPayment`
2. **Customer pays offline** (bank transfer, cash, etc.)
3. **Admin records payment** → `POST /bookings/{id}/pay` with optional `paymentReference`
4. **Status moves** to `PendingVerification`
5. **Admin confirms** → `POST /bookings/{id}/confirm` → Status: `Confirmed`

### Payment Webhook Endpoint

```
POST /webhooks/payments/{provider}
```

This endpoint is called by the payment provider (e.g., PayMongo) with their native webhook payload. Chronith processes it internally using the pluggable `IPaymentProvider` interface.

**PaymentEventType values:** `Success`, `Failed`, `Expired`, `Cancelled`

### Idempotency

Payment operations use `IdempotencyKey` to prevent duplicate processing. The system deduplicates based on the provider's transaction ID.

---

## 8. Webhook & Event System

### How It Works

Chronith uses an **outbox pattern** for reliable webhook delivery:

1. When a booking event occurs, an outbox entry is written to the database in the same transaction.
2. A background service (`WebhookDeliveryService`) polls for pending entries and delivers them via HTTP POST.
3. Delivery is retried with exponential backoff on failure.
4. After max retries, entries are marked as `Abandoned`.

### Outbox Statuses

| Status      | Description                       |
| ----------- | --------------------------------- |
| `Pending`   | Awaiting delivery                 |
| `Delivered` | Successfully delivered (HTTP 2xx) |
| `Failed`    | Delivery failed, will retry       |
| `Abandoned` | Max retries exceeded              |

### Outbox Categories

| Category           | Description                                        |
| ------------------ | -------------------------------------------------- |
| `TenantWebhook`    | Delivered to tenant-registered webhook URLs        |
| `CustomerCallback` | Delivered to `customerCallbackUrl` on booking type |
| `Notification`     | Internal notification delivery (email/SMS/push)    |

### Webhook Payload Shape

```json
{
  "id": "outbox-entry-uuid",
  "eventType": "booking.confirmed",
  "tenantId": "t1e2n3...",
  "bookingTypeId": "bt-uuid-...",
  "bookingTypeSlug": "photography-session",
  "data": {
    "bookingId": "b1o2o3k4-...",
    "fromStatus": "PendingVerification",
    "toStatus": "Confirmed",
    "start": "2026-03-20T10:00:00+08:00",
    "end": "2026-03-20T11:00:00+08:00",
    "customerId": "c1u2s3t4-...",
    "customerEmail": "customer@example.com"
  },
  "timestamp": "2026-03-20T10:05:00+08:00"
}
```

### Event Types

| Event Type               | Trigger                                         |
| ------------------------ | ----------------------------------------------- |
| `booking.created`        | New booking created                             |
| `booking.paid`           | Payment recorded (status → PendingVerification) |
| `booking.confirmed`      | Booking confirmed                               |
| `booking.cancelled`      | Booking cancelled                               |
| `booking.rescheduled`    | Booking time changed                            |
| `booking.staff_assigned` | Staff member assigned to booking                |

### Webhook Secret

When registering a webhook, you provide a `secret` (min 16 chars). This can be used by your receiver to verify the authenticity of incoming webhook payloads.

---

## 9. Error Handling

### Standard Error Response

All errors follow a consistent JSON shape:

```json
{
  "statusCode": 400,
  "message": "Validation failed",
  "errors": {
    "customerEmail": ["'Customer Email' must not be empty."],
    "startTime": ["'Start Time' must not be empty."]
  }
}
```

### Error Code Mapping

| HTTP Status | Exception Type                       | When                                      |
| ----------- | ------------------------------------ | ----------------------------------------- |
| `400`       | Validation errors (FluentValidation) | Missing/invalid request fields            |
| `401`       | Unauthorized                         | Missing or expired auth token             |
| `403`       | `UnauthorizedException`              | Insufficient role/permissions             |
| `404`       | `NotFoundException`                  | Entity not found (or not in tenant scope) |
| `409`       | `ConflictException`                  | Duplicate slug, duplicate entity          |
| `409`       | `SlotConflictException`              | Slot already booked at capacity           |
| `422`       | `InvalidStateTransitionException`    | Invalid booking status transition         |
| `422`       | `CustomFieldValidationException`     | Custom field values don't match schema    |
| `429`       | Rate limit exceeded                  | Too many requests (see Section 13)        |

### Handling Slot Conflicts (409)

When creating a booking and the slot is full:

```json
{
  "statusCode": 409,
  "message": "The requested time slot conflicts with an existing booking."
}
```

**Recommended handling:** Show the user a message, refresh availability, and let them pick a different slot. Or offer to join the waitlist.

### Handling State Transition Errors (422)

When attempting an invalid status transition (e.g., confirming a `PendingPayment` booking):

```json
{
  "statusCode": 422,
  "message": "Cannot transition from PendingPayment to Confirmed."
}
```

---

## 10. Integration Code Examples

### 10.1 Quick Start — Server-to-Server (TypeScript)

```typescript
const BASE_URL = "https://api.chronith.io";
const API_KEY = "ck_live_abc123def456...";

const headers = {
  "Content-Type": "application/json",
  "X-Api-Key": API_KEY,
};

// 1. Create a booking type
const bookingType = await fetch(`${BASE_URL}/booking-types`, {
  method: "POST",
  headers,
  body: JSON.stringify({
    slug: "haircut",
    name: "Haircut",
    isTimeSlot: true,
    capacity: 1,
    paymentMode: "Manual",
    priceInCentavos: 50000, // PHP 500.00
    currency: "PHP",
    requiresStaffAssignment: false,
    durationMinutes: 30,
    bufferBeforeMinutes: 5,
    bufferAfterMinutes: 5,
    availabilityWindows: [
      { dayOfWeek: "Monday", startTime: "09:00:00", endTime: "18:00:00" },
      { dayOfWeek: "Tuesday", startTime: "09:00:00", endTime: "18:00:00" },
      { dayOfWeek: "Wednesday", startTime: "09:00:00", endTime: "18:00:00" },
      { dayOfWeek: "Thursday", startTime: "09:00:00", endTime: "18:00:00" },
      { dayOfWeek: "Friday", startTime: "09:00:00", endTime: "18:00:00" },
      { dayOfWeek: "Saturday", startTime: "10:00:00", endTime: "16:00:00" },
    ],
  }),
}).then((r) => r.json());

// 2. Check availability for a date
const availability = await fetch(
  `${BASE_URL}/booking-types/haircut/availability?date=2026-03-20`,
  { headers },
).then((r) => r.json());

console.log("Available slots:", availability.slots);

// 3. Create a booking in the first available slot
const slot = availability.slots[0];
const booking = await fetch(`${BASE_URL}/bookings`, {
  method: "POST",
  headers,
  body: JSON.stringify({
    bookingTypeSlug: "haircut",
    startTime: slot.start,
    customerEmail: "maria@example.com",
  }),
}).then((r) => r.json());

console.log("Booking created:", booking.id, "Status:", booking.status);
// → Status: "PendingPayment" (since price > 0)
```

### 10.2 Quick Start — Server-to-Server (Python)

```python
import httpx

BASE_URL = "https://api.chronith.io"
API_KEY = "ck_live_abc123def456..."

client = httpx.Client(
    base_url=BASE_URL,
    headers={"X-Api-Key": API_KEY, "Content-Type": "application/json"},
)

# Check availability
resp = client.get("/booking-types/haircut/availability", params={"date": "2026-03-20"})
slots = resp.json()["slots"]

# Create a booking
booking = client.post("/bookings", json={
    "bookingTypeSlug": "haircut",
    "startTime": slots[0]["start"],
    "customerEmail": "maria@example.com",
}).json()

print(f"Booking {booking['id']} — Status: {booking['status']}")
```

### 10.3 Quick Start — Server-to-Server (C#)

```csharp
using var http = new HttpClient { BaseAddress = new Uri("https://api.chronith.io") };
http.DefaultRequestHeaders.Add("X-Api-Key", "ck_live_abc123def456...");

// Check availability
var availability = await http.GetFromJsonAsync<AvailabilityDto>(
    "/booking-types/haircut/availability?date=2026-03-20");

// Create booking
var response = await http.PostAsJsonAsync("/bookings", new
{
    BookingTypeSlug = "haircut",
    StartTime = availability.Slots[0].Start,
    CustomerEmail = "maria@example.com"
});

var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
Console.WriteLine($"Booking {booking.Id} — Status: {booking.Status}");
```

### 10.4 JWT Auth with Auto-Refresh (TypeScript / Axios)

```typescript
import axios, { AxiosInstance } from "axios";

class ChronithClient {
  private client: AxiosInstance;
  private accessToken: string = "";
  private refreshToken: string = "";

  constructor(private baseUrl: string) {
    this.client = axios.create({ baseURL: baseUrl });

    // Auto-refresh interceptor
    this.client.interceptors.response.use(
      (res) => res,
      async (error) => {
        if (error.response?.status === 401 && this.refreshToken) {
          await this.refresh();
          error.config.headers.Authorization = `Bearer ${this.accessToken}`;
          return this.client.request(error.config);
        }
        throw error;
      },
    );
  }

  async login(tenantSlug: string, email: string, password: string) {
    const { data } = await this.client.post("/auth/login", {
      tenantSlug,
      email,
      password,
    });
    this.accessToken = data.accessToken;
    this.refreshToken = data.refreshToken;
    this.client.defaults.headers.common.Authorization = `Bearer ${this.accessToken}`;
  }

  private async refresh() {
    const { data } = await this.client.post("/auth/refresh", {
      refreshToken: this.refreshToken,
    });
    this.accessToken = data.accessToken;
    this.refreshToken = data.refreshToken;
    this.client.defaults.headers.common.Authorization = `Bearer ${this.accessToken}`;
  }

  async getBookings() {
    return (await this.client.get("/bookings")).data;
  }

  async confirmBooking(id: string) {
    return (await this.client.post(`/bookings/${id}/confirm`)).data;
  }
}

// Usage
const client = new ChronithClient("https://api.chronith.io");
await client.login("acme-studio", "admin@acme.com", "password");
const bookings = await client.getBookings();
```

### 10.5 Public Booking Flow (Browser JavaScript)

```typescript
const TENANT_SLUG = "acme-studio";
const API = `https://api.chronith.io/public/${TENANT_SLUG}`;

// Step 1: Fetch booking types
const bookingTypes = await fetch(`${API}/booking-types`).then((r) => r.json());
// → [{ slug: "photography-session", name: "Photography Session", ... }]

// Step 2: User selects a booking type, fetch detail
const selected = bookingTypes[0];
const detail = await fetch(`${API}/booking-types/${selected.slug}`).then((r) =>
  r.json(),
);
// → { customFieldSchema: "...", priceInCentavos: 250000, ... }

// Step 3: Check availability for selected date
const slots = await fetch(
  `${API}/booking-types/${selected.slug}/availability?date=2026-03-20`,
).then((r) => r.json());
// → { slots: [{ start: "...", end: "..." }, ...] }

// Step 4: User picks a slot, create booking
const booking = await fetch(`${API}/booking-types/${selected.slug}/bookings`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    startTime: slots.slots[0].start,
    customerEmail: "customer@example.com",
    customerId: "customer-uuid-if-logged-in",
  }),
}).then((r) => r.json());

// Step 5: Handle payment (if required)
if (booking.checkoutUrl) {
  // Redirect to payment provider
  window.location.href = booking.checkoutUrl;
} else if (booking.status === "PendingVerification") {
  // Free booking — show "awaiting confirmation" message
  showMessage("Your booking is pending confirmation!");
}
```

### 10.6 Webhook Receiver (Node.js / Express)

```typescript
import express from "express";

const app = express();
app.use(express.json());

// Track processed events for idempotency
const processedEvents = new Set<string>();

app.post("/webhooks/chronith", (req, res) => {
  const event = req.body;

  // Idempotency check
  if (processedEvents.has(event.id)) {
    return res.status(200).json({ status: "already_processed" });
  }

  switch (event.eventType) {
    case "booking.created":
      console.log(`New booking: ${event.data.bookingId}`);
      // Sync to your database, send notification, etc.
      break;

    case "booking.paid":
      console.log(`Payment received for: ${event.data.bookingId}`);
      // Update your order management system
      break;

    case "booking.confirmed":
      console.log(`Booking confirmed: ${event.data.bookingId}`);
      // Send confirmation email, update calendar, etc.
      break;

    case "booking.cancelled":
      console.log(`Booking cancelled: ${event.data.bookingId}`);
      // Process refund, free up resources, etc.
      break;

    case "booking.rescheduled":
      console.log(`Booking rescheduled: ${event.data.bookingId}`);
      // Update calendar entries
      break;

    default:
      console.log(`Unknown event: ${event.eventType}`);
  }

  processedEvents.add(event.id);
  res.status(200).json({ status: "ok" });
});
```

### 10.7 Payment Flow (TypeScript)

```typescript
// === Automatic Payment (PayMongo) ===

// 1. Create booking — checkoutUrl is returned
const booking = await fetch(`${BASE_URL}/bookings`, {
  method: "POST",
  headers,
  body: JSON.stringify({
    bookingTypeSlug: "photography-session",
    startTime: "2026-03-20T10:00:00+08:00",
    customerEmail: "customer@example.com",
  }),
}).then((r) => r.json());

if (booking.checkoutUrl) {
  // 2. Redirect customer to payment page
  console.log("Redirect to:", booking.checkoutUrl);
  // After payment, PayMongo calls POST /webhooks/payments/PayMongo
  // System automatically moves to PendingVerification
}

// === Manual Payment ===

// 1. Customer pays offline (bank transfer, GCash, etc.)
// 2. Admin records the payment
const paid = await fetch(`${BASE_URL}/bookings/${booking.id}/pay`, {
  method: "POST",
  headers,
  body: JSON.stringify({
    bookingId: booking.id,
    paymentReference: "GCASH-REF-123456",
  }),
}).then((r) => r.json());
// → status: "PendingVerification"

// 3. Admin confirms
const confirmed = await fetch(`${BASE_URL}/bookings/${booking.id}/confirm`, {
  method: "POST",
  headers,
}).then((r) => r.json());
// → status: "Confirmed"
```

### 10.8 Staff Management (TypeScript)

```typescript
// Create staff member with availability
const staff = await fetch(`${BASE_URL}/staff`, {
  method: "POST",
  headers,
  body: JSON.stringify({
    name: "Ana Reyes",
    email: "ana@acme.com",
    availabilityWindows: [
      { dayOfWeek: "Monday", startTime: "09:00:00", endTime: "17:00:00" },
      { dayOfWeek: "Tuesday", startTime: "09:00:00", endTime: "17:00:00" },
      { dayOfWeek: "Thursday", startTime: "09:00:00", endTime: "17:00:00" },
    ],
  }),
}).then((r) => r.json());

// Check staff availability for a date
const staffSlots = await fetch(
  `${BASE_URL}/staff/${staff.id}/availability?date=2026-03-20`,
  { headers },
).then((r) => r.json());

// Assign staff to a booking
await fetch(`${BASE_URL}/bookings/${bookingId}/assign-staff`, {
  method: "POST",
  headers,
  body: JSON.stringify({
    bookingId: bookingId,
    staffMemberId: staff.id,
  }),
});

// Block staff availability (e.g., training day)
await fetch(`${BASE_URL}/time-blocks`, {
  method: "POST",
  headers,
  body: JSON.stringify({
    staffMemberId: staff.id,
    start: "2026-03-25T09:00:00+08:00",
    end: "2026-03-25T17:00:00+08:00",
    reason: "Staff training day",
  }),
});
```

### 10.9 Analytics (TypeScript)

```typescript
// Booking analytics
const bookingStats = await fetch(
  `${BASE_URL}/analytics/bookings?from=2026-03-01&to=2026-03-31`,
  { headers },
).then((r) => r.json());
// → { total: 150, byStatus: { Confirmed: 120, ... }, timeSeries: [...] }

// Revenue analytics
const revenue = await fetch(
  `${BASE_URL}/analytics/revenue?from=2026-03-01&to=2026-03-31`,
  { headers },
).then((r) => r.json());
// → { totalCentavos: 37500000, currency: "PHP", byBookingType: [...] }

// Export to CSV
const csvBlob = await fetch(
  `${BASE_URL}/analytics/export?format=csv&from=2026-03-01&to=2026-03-31`,
  { headers },
).then((r) => r.blob());
```

### 10.10 Error Handling (TypeScript)

```typescript
class ChronithApiError extends Error {
  constructor(
    public statusCode: number,
    message: string,
    public errors?: Record<string, string[]>,
  ) {
    super(message);
  }
}

async function chronithFetch(url: string, options: RequestInit = {}) {
  const response = await fetch(url, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...headers,
      ...options.headers,
    },
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));

    switch (response.status) {
      case 400:
        throw new ChronithApiError(400, "Validation failed", body.errors);
      case 401:
        throw new ChronithApiError(401, "Authentication required");
      case 403:
        throw new ChronithApiError(403, "Insufficient permissions");
      case 404:
        throw new ChronithApiError(404, body.message || "Not found");
      case 409:
        throw new ChronithApiError(
          409,
          body.message || "Conflict — slot may be taken",
        );
      case 422:
        throw new ChronithApiError(
          422,
          body.message || "Invalid state transition",
        );
      case 429:
        const retryAfter = response.headers.get("Retry-After");
        throw new ChronithApiError(
          429,
          `Rate limited. Retry after ${retryAfter}s`,
        );
      default:
        throw new ChronithApiError(
          response.status,
          body.message || "Unknown error",
        );
    }
  }

  return response.json();
}

// Usage with retry for slot conflicts
async function createBookingWithRetry(
  slug: string,
  startTime: string,
  email: string,
  maxRetries = 3,
) {
  for (let attempt = 0; attempt < maxRetries; attempt++) {
    try {
      return await chronithFetch(`${BASE_URL}/bookings`, {
        method: "POST",
        body: JSON.stringify({
          bookingTypeSlug: slug,
          startTime,
          customerEmail: email,
        }),
      });
    } catch (err) {
      if (
        err instanceof ChronithApiError &&
        err.statusCode === 409 &&
        attempt < maxRetries - 1
      ) {
        // Slot taken — refresh availability and pick next slot
        const avail = await chronithFetch(
          `${BASE_URL}/booking-types/${slug}/availability?date=${startTime.split("T")[0]}`,
        );
        if (avail.slots.length > 0) {
          startTime = avail.slots[0].start;
          continue;
        }
      }
      throw err;
    }
  }
}
```

---

## 11. Integration Patterns

### 11.1 Embedded Booking Widget (Frontend SPA)

**Scenario:** You embed a booking flow directly in your website (React, Vue, vanilla JS). The widget runs in the browser and uses only the **Public API** — no server-side component required.

**Architecture:**

```
┌──────────────────────────────────────────────────┐
│  Your Website (React / Vue / Vanilla JS)          │
│                                                    │
│  ┌──────────────────────────────────────────────┐ │
│  │  Booking Widget Component                     │ │
│  │                                               │ │
│  │  1. GET /public/{slug}/booking-types          │ │
│  │  2. GET /public/{slug}/booking-types/         │ │
│  │     {btSlug}/availability?date=...            │ │
│  │  3. POST /public/{slug}/booking-types/        │ │
│  │     {btSlug}/bookings                         │ │
│  │  4. Redirect → checkoutUrl (if paid)          │ │
│  └──────────────────────────────────────────────┘ │
│                          │                         │
│                          ▼                         │
│              Chronith Public API                   │
└──────────────────────────────────────────────────┘
```

**Key considerations:**

- All requests go to `/public/{tenantSlug}/...` — no auth headers needed.
- CORS: Chronith allows cross-origin requests to public endpoints.
- The widget should handle `409 SlotConflict` gracefully — refresh slots and let the user re-pick.
- For paid bookings, redirect to `checkoutUrl`. After payment, the provider redirects back to your `successUrl` / `cancelUrl` (configured on the booking type).
- Optionally pass `customerId` if the user is logged into your system (enables customer auth features like viewing their bookings).

**React example (hooks-based):**

```tsx
import { useState, useEffect } from "react";

const API = "https://api.chronith.io/public/acme-studio";

function BookingWidget({ bookingTypeSlug }: { bookingTypeSlug: string }) {
  const [slots, setSlots] = useState<{ start: string; end: string }[]>([]);
  const [date, setDate] = useState("2026-03-20");
  const [status, setStatus] = useState<
    "idle" | "loading" | "success" | "error"
  >("idle");

  // Fetch availability when date changes
  useEffect(() => {
    fetch(`${API}/booking-types/${bookingTypeSlug}/availability?date=${date}`)
      .then((r) => r.json())
      .then((data) => setSlots(data.slots))
      .catch(() => setSlots([]));
  }, [date, bookingTypeSlug]);

  async function bookSlot(slot: { start: string }) {
    setStatus("loading");
    try {
      const res = await fetch(
        `${API}/booking-types/${bookingTypeSlug}/bookings`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            startTime: slot.start,
            customerEmail: "customer@example.com", // from your auth context
          }),
        },
      );

      if (res.status === 409) {
        // Slot conflict — refresh availability
        const updated = await fetch(
          `${API}/booking-types/${bookingTypeSlug}/availability?date=${date}`,
        ).then((r) => r.json());
        setSlots(updated.slots);
        setStatus("error");
        return;
      }

      const booking = await res.json();

      if (booking.checkoutUrl) {
        window.location.href = booking.checkoutUrl; // Redirect to pay
      } else {
        setStatus("success"); // Free booking — pending verification
      }
    } catch {
      setStatus("error");
    }
  }

  return (
    <div>
      <input
        type="date"
        value={date}
        onChange={(e) => setDate(e.target.value)}
      />
      {slots.map((slot) => (
        <button
          key={slot.start}
          onClick={() => bookSlot(slot)}
          disabled={status === "loading"}
        >
          {new Date(slot.start).toLocaleTimeString()} –{" "}
          {new Date(slot.end).toLocaleTimeString()}
        </button>
      ))}
      {status === "success" && <p>Booking submitted! Awaiting confirmation.</p>}
      {status === "error" && (
        <p>That slot is no longer available. Please pick another.</p>
      )}
    </div>
  );
}
```

### 11.2 Backend Orchestrator (Server-to-Server)

**Scenario:** Your backend manages bookings on behalf of users. You call the Chronith API from your server using an **API Key** or **JWT**. This pattern is typical for SaaS platforms, CRMs, or internal tools.

**Architecture:**

```
┌──────────────┐     ┌──────────────────┐     ┌──────────────┐
│  Your Users   │────►│  Your Backend     │────►│  Chronith    │
│  (Browser/    │◄────│  (Node/Python/    │◄────│  API         │
│   Mobile)     │     │   C#/.NET)        │     │              │
└──────────────┘     └──────────────────┘     └──────────────┘
                            │       ▲
                            │       │
                      Webhook ◄─────┘
                      POST /your-webhook
```

**Key considerations:**

- Use **API Key** (`X-Api-Key`) for server-to-server calls — simpler than JWT, no token refresh needed.
- Your backend proxies/aggregates data from Chronith and your own DB.
- Register webhooks to receive real-time booking events and sync state.
- Implement idempotency keys on your side for any booking creation retries.

**Example pattern (Node.js backend):**

```typescript
// routes/bookings.ts — Your backend wraps Chronith API
import express from "express";

const router = express.Router();
const CHRONITH = "https://api.chronith.io";
const API_KEY = process.env.CHRONITH_API_KEY!;

const chronithHeaders = {
  "Content-Type": "application/json",
  "X-Api-Key": API_KEY,
};

// Your users hit YOUR API, your backend calls Chronith
router.post("/book", async (req, res) => {
  const { bookingTypeSlug, date, slotIndex, userId } = req.body;

  // 1. Look up the user in YOUR database
  const user = await db.users.findById(userId);

  // 2. Check availability on Chronith
  const availRes = await fetch(
    `${CHRONITH}/booking-types/${bookingTypeSlug}/availability?date=${date}`,
    { headers: chronithHeaders },
  );
  const { slots } = await availRes.json();

  if (!slots[slotIndex]) {
    return res.status(400).json({ error: "Invalid slot" });
  }

  // 3. Create booking on Chronith
  const bookingRes = await fetch(`${CHRONITH}/bookings`, {
    method: "POST",
    headers: chronithHeaders,
    body: JSON.stringify({
      bookingTypeSlug,
      startTime: slots[slotIndex].start,
      customerEmail: user.email,
    }),
  });

  const booking = await bookingRes.json();

  // 4. Save reference in YOUR database
  await db.bookings.create({
    userId,
    chronithBookingId: booking.id,
    status: booking.status,
  });

  res.json({ booking });
});

// Webhook receiver — keeps your DB in sync
router.post("/webhooks/chronith", async (req, res) => {
  const event = req.body;
  const localBooking = await db.bookings.findByChronithId(event.data.bookingId);

  if (localBooking) {
    await db.bookings.update(localBooking.id, {
      status: event.data.toStatus || event.eventType.split(".")[1],
    });
  }

  res.status(200).json({ status: "ok" });
});
```

### 11.3 Mobile App Integration (React Native / Flutter)

**Scenario:** A mobile app allows customers to browse and book. Uses **Customer Auth** for personalized features (view my bookings) and the **Public API** for browsing.

**Architecture:**

```
┌────────────────────┐            ┌──────────────┐
│  Mobile App         │            │  Chronith    │
│  (React Native /    │───────────►│  API         │
│   Flutter / Swift)  │◄───────────│              │
│                     │            └──────────────┘
│  Auth flow:         │
│  1. POST /public/{slug}/auth/login    (customer auth)
│  2. Store tokens in secure storage
│  3. Attach Bearer token to requests
│  4. Auto-refresh on 401
└────────────────────┘
```

**Key considerations:**

- Use **Customer Auth** (`POST /public/{tenantSlug}/auth/login`) — separate from admin JWT.
- Store tokens in platform-secure storage (iOS Keychain, Android EncryptedSharedPreferences).
- Public endpoints (`/public/...`) for browsing don't need auth.
- Customer-authenticated endpoints let users view their own bookings, cancel, etc.
- Handle offline gracefully — cache availability locally, queue booking attempts.

**React Native example:**

```typescript
import * as SecureStore from "expo-secure-store";

const TENANT = "acme-studio";
const API = `https://api.chronith.io/public/${TENANT}`;

class ChronithMobileClient {
  private accessToken: string | null = null;

  async init() {
    this.accessToken = await SecureStore.getItemAsync("chronith_access_token");
  }

  private authHeaders(): HeadersInit {
    return this.accessToken
      ? {
          Authorization: `Bearer ${this.accessToken}`,
          "Content-Type": "application/json",
        }
      : { "Content-Type": "application/json" };
  }

  async customerLogin(email: string, password: string) {
    const res = await fetch(`${API}/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
    });
    const data = await res.json();
    this.accessToken = data.accessToken;
    await SecureStore.setItemAsync("chronith_access_token", data.accessToken);
    await SecureStore.setItemAsync("chronith_refresh_token", data.refreshToken);
    return data;
  }

  async getBookingTypes() {
    return fetch(`${API}/booking-types`).then((r) => r.json());
  }

  async getAvailability(slug: string, date: string) {
    return fetch(`${API}/booking-types/${slug}/availability?date=${date}`).then(
      (r) => r.json(),
    );
  }

  async createBooking(slug: string, startTime: string, email: string) {
    return fetch(`${API}/booking-types/${slug}/bookings`, {
      method: "POST",
      headers: this.authHeaders(),
      body: JSON.stringify({ startTime, customerEmail: email }),
    }).then((r) => r.json());
  }
}
```

### 11.4 Calendar Sync (Google Calendar / Outlook / Apple)

**Scenario:** Subscribe to confirmed bookings as an iCal feed in your calendar app, or push events programmatically via Google Calendar API.

#### Option A: iCal Subscription (Zero Code)

Chronith provides an iCal feed per booking type:

```
GET /booking-types/{slug}/calendar.ics
```

Subscribe to this URL in any calendar app:

| Calendar        | How to Subscribe                                          |
| --------------- | --------------------------------------------------------- |
| Google Calendar | Settings → Add calendar → From URL → paste the `.ics` URL |
| Outlook         | Add calendar → Subscribe from web → paste the `.ics` URL  |
| Apple Calendar  | File → New Calendar Subscription → paste the `.ics` URL   |

The feed auto-updates as bookings are confirmed or cancelled.

#### Option B: Programmatic Sync via Webhooks

For two-way sync or richer integration, use webhooks to push events to Google Calendar API:

```typescript
import { google } from "googleapis";

const calendar = google.calendar({ version: "v3", auth: oauthClient });

// Webhook handler — push confirmed bookings to Google Calendar
app.post("/webhooks/chronith", async (req, res) => {
  const event = req.body;

  if (event.eventType === "booking.confirmed") {
    await calendar.events.insert({
      calendarId: "primary",
      requestBody: {
        summary: `Booking: ${event.bookingTypeSlug}`,
        description: `Customer: ${event.data.customerEmail}\nBooking ID: ${event.data.bookingId}`,
        start: {
          dateTime: event.data.start,
          timeZone: "Asia/Manila",
        },
        end: {
          dateTime: event.data.end,
          timeZone: "Asia/Manila",
        },
      },
    });
  }

  if (event.eventType === "booking.cancelled") {
    // Find and delete the calendar event by booking ID stored in description
    const events = await calendar.events.list({
      calendarId: "primary",
      q: event.data.bookingId,
    });
    for (const calEvent of events.data.items || []) {
      await calendar.events.delete({
        calendarId: "primary",
        eventId: calEvent.id!,
      });
    }
  }

  res.status(200).json({ status: "ok" });
});
```

---

## 12. SDK Usage

> **Note:** These SDKs are convenience wrappers around the REST API documented above. You can always call the API directly — the SDKs are not required.

### 12.1 TypeScript SDK (`@chronith/sdk`)

#### Installation

```bash
npm install @chronith/sdk
# or
yarn add @chronith/sdk
# or
pnpm add @chronith/sdk
```

#### Configuration

```typescript
import { ChronithClient } from "@chronith/sdk";

// API Key authentication (server-to-server)
const client = new ChronithClient({
  baseUrl: "https://api.chronith.io",
  apiKey: "ck_live_abc123def456...",
});

// JWT authentication (admin/staff)
const adminClient = new ChronithClient({
  baseUrl: "https://api.chronith.io",
  auth: {
    tenantSlug: "acme-studio",
    email: "admin@acme.com",
    password: "secure-password",
  },
  autoRefresh: true, // Automatically refresh JWT on 401
});
```

#### Booking Types

```typescript
// List booking types
const types = await client.bookingTypes.list();

// Get a single booking type
const haircut = await client.bookingTypes.get("haircut");

// Create a booking type
const newType = await client.bookingTypes.create({
  slug: "consultation",
  name: "Consultation",
  isTimeSlot: true,
  capacity: 1,
  paymentMode: "Manual",
  priceInCentavos: 150000, // PHP 1,500.00
  currency: "PHP",
  requiresStaffAssignment: true,
  durationMinutes: 60,
  bufferBeforeMinutes: 10,
  bufferAfterMinutes: 10,
  availabilityWindows: [
    { dayOfWeek: "Monday", startTime: "09:00:00", endTime: "17:00:00" },
    { dayOfWeek: "Wednesday", startTime: "09:00:00", endTime: "17:00:00" },
    { dayOfWeek: "Friday", startTime: "09:00:00", endTime: "17:00:00" },
  ],
});

// Update
await client.bookingTypes.update("consultation", {
  priceInCentavos: 200000, // Raise to PHP 2,000.00
});

// Delete (soft delete)
await client.bookingTypes.delete("consultation");
```

#### Availability & Bookings

```typescript
// Check availability
const availability = await client.availability.check("haircut", "2026-03-20");
console.log(availability.slots); // [{ start, end }, ...]

// Create booking
const booking = await client.bookings.create({
  bookingTypeSlug: "haircut",
  startTime: availability.slots[0].start,
  customerEmail: "maria@example.com",
});

// List bookings with filters
const bookings = await client.bookings.list({
  status: "PendingPayment",
  from: "2026-03-01",
  to: "2026-03-31",
  page: 1,
  pageSize: 25,
});

// Status transitions
await client.bookings.pay(booking.id, { paymentReference: "GCASH-123" });
await client.bookings.confirm(booking.id);
await client.bookings.cancel(booking.id);

// Reschedule
await client.bookings.reschedule(booking.id, {
  newStartTime: "2026-03-21T14:00:00+08:00",
});

// Assign staff
await client.bookings.assignStaff(booking.id, { staffMemberId: "staff-uuid" });
```

#### Staff

```typescript
// CRUD
const staff = await client.staff.create({
  name: "Ana Reyes",
  email: "ana@acme.com",
  availabilityWindows: [
    { dayOfWeek: "Monday", startTime: "09:00:00", endTime: "17:00:00" },
  ],
});
const allStaff = await client.staff.list();
await client.staff.update(staff.id, { name: "Ana M. Reyes" });
await client.staff.deactivate(staff.id);

// Staff availability
const staffSlots = await client.staff.availability(staff.id, "2026-03-20");
```

#### Webhooks

```typescript
// Register a webhook
const webhook = await client.webhooks.create({
  bookingTypeSlug: "haircut",
  url: "https://yourapp.com/webhooks/chronith",
  secret: "whsec_a1b2c3d4e5f6g7h8",
});

// List delivery history
const deliveries = await client.webhooks.deliveries(webhook.id);

// Retry a failed delivery
await client.webhooks.retryDelivery(webhook.id, deliveries[0].id);
```

#### Analytics

```typescript
const bookingAnalytics = await client.analytics.bookings({
  from: "2026-03-01",
  to: "2026-03-31",
});

const revenue = await client.analytics.revenue({
  from: "2026-03-01",
  to: "2026-03-31",
});

const utilization = await client.analytics.utilization({
  from: "2026-03-01",
  to: "2026-03-31",
});

// Export
const csvBuffer = await client.analytics.export({
  format: "csv",
  from: "2026-03-01",
  to: "2026-03-31",
});
```

#### Public Client (No Auth)

```typescript
import { ChronithPublicClient } from "@chronith/sdk";

const publicClient = new ChronithPublicClient({
  baseUrl: "https://api.chronith.io",
  tenantSlug: "acme-studio",
});

const types = await publicClient.bookingTypes.list();
const slots = await publicClient.availability.check("haircut", "2026-03-20");
const booking = await publicClient.bookings.create("haircut", {
  startTime: slots.slots[0].start,
  customerEmail: "customer@example.com",
});
```

### 12.2 C# SDK (`Chronith.Client`)

#### Installation

```bash
dotnet add package Chronith.Client
```

#### DI Registration (ASP.NET Core)

```csharp
// Program.cs or Startup.cs
builder.Services.AddChronithClient(options =>
{
    options.BaseUrl = "https://api.chronith.io";
    options.ApiKey = "ck_live_abc123def456...";
});
```

#### Usage via Dependency Injection

```csharp
public class BookingService(IChronithClient chronith)
{
    public async Task<BookingDto> CreateBookingAsync(string slug, string email)
    {
        // Check availability
        var availability = await chronith.Availability.CheckAsync(slug, DateOnly.Parse("2026-03-20"));

        if (availability.Slots.Count == 0)
            throw new InvalidOperationException("No available slots");

        // Create booking
        return await chronith.Bookings.CreateAsync(new CreateBookingRequest
        {
            BookingTypeSlug = slug,
            StartTime = availability.Slots[0].Start,
            CustomerEmail = email
        });
    }

    public async Task ConfirmAndNotifyAsync(Guid bookingId)
    {
        var booking = await chronith.Bookings.PayAsync(bookingId, new PayBookingRequest
        {
            PaymentReference = "BANK-TRANSFER-REF-001"
        });

        booking = await chronith.Bookings.ConfirmAsync(bookingId);
        // booking.Status == "Confirmed"
    }
}
```

#### Staff Management

```csharp
public class StaffService(IChronithClient chronith)
{
    public async Task<StaffMemberDto> OnboardStaffAsync(string name, string email)
    {
        var staff = await chronith.Staff.CreateAsync(new CreateStaffRequest
        {
            Name = name,
            Email = email,
            AvailabilityWindows =
            [
                new() { DayOfWeek = "Monday", StartTime = "09:00:00", EndTime = "17:00:00" },
                new() { DayOfWeek = "Tuesday", StartTime = "09:00:00", EndTime = "17:00:00" },
                new() { DayOfWeek = "Wednesday", StartTime = "09:00:00", EndTime = "17:00:00" },
            ]
        });

        return staff;
    }

    public async Task BlockDayOffAsync(Guid staffId, DateTimeOffset date, string reason)
    {
        await chronith.TimeBlocks.CreateAsync(new CreateTimeBlockRequest
        {
            StaffMemberId = staffId,
            Start = date.Date,
            End = date.Date.AddDays(1),
            Reason = reason
        });
    }
}
```

#### Analytics

```csharp
var bookingStats = await chronith.Analytics.BookingsAsync(
    from: DateOnly.Parse("2026-03-01"),
    to: DateOnly.Parse("2026-03-31"));

Console.WriteLine($"Total bookings: {bookingStats.Total}");
Console.WriteLine($"Confirmed: {bookingStats.ByStatus["Confirmed"]}");

var revenue = await chronith.Analytics.RevenueAsync(
    from: DateOnly.Parse("2026-03-01"),
    to: DateOnly.Parse("2026-03-31"));

Console.WriteLine($"Total revenue: PHP {revenue.TotalCentavos / 100m:N2}");
```

---

## 13. Rate Limiting & Operational Notes

### 13.1 Rate Limit Policies

Chronith enforces per-tenant rate limits to ensure fair usage across the multi-tenant platform. Limits are applied by `TenantId` for authenticated requests and by client IP for unauthenticated requests.

| Policy        | Scope      | Window    | Limit        | Applies To                                             |
| ------------- | ---------- | --------- | ------------ | ------------------------------------------------------ |
| Authenticated | `TenantId` | 1 minute  | 600 requests | All authenticated API endpoints                        |
| Public        | Client IP  | 1 minute  | 120 requests | `/public/...` endpoints                                |
| Auth          | Client IP  | 5 minutes | 20 requests  | `/auth/login`, `/auth/refresh`, `/public/.../auth/...` |
| Export        | `TenantId` | 1 hour    | 10 requests  | `/analytics/export`, `/audit/export`                   |

### 13.2 Rate Limit Response

When a rate limit is exceeded, the API returns:

```
HTTP/1.1 429 Too Many Requests
Retry-After: 45
Content-Type: application/json
```

```json
{
  "statusCode": 429,
  "message": "Rate limit exceeded. Try again in 45 seconds."
}
```

**Headers on all responses:**

| Header                  | Description                                    |
| ----------------------- | ---------------------------------------------- |
| `X-RateLimit-Limit`     | Maximum requests allowed in the current window |
| `X-RateLimit-Remaining` | Requests remaining in the current window       |
| `X-RateLimit-Reset`     | UTC epoch seconds when the window resets       |
| `Retry-After`           | Seconds to wait (only on 429 responses)        |

### 13.3 Handling Rate Limits in Code

```typescript
async function fetchWithRateLimit(
  url: string,
  options: RequestInit = {},
): Promise<Response> {
  const maxRetries = 3;

  for (let attempt = 0; attempt < maxRetries; attempt++) {
    const response = await fetch(url, options);

    if (response.status !== 429) {
      return response;
    }

    const retryAfter = parseInt(
      response.headers.get("Retry-After") || "60",
      10,
    );
    console.warn(
      `Rate limited. Retrying in ${retryAfter}s (attempt ${attempt + 1}/${maxRetries})`,
    );
    await new Promise((resolve) => setTimeout(resolve, retryAfter * 1000));
  }

  throw new Error("Max retries exceeded due to rate limiting");
}
```

### 13.4 Pagination

All list endpoints support cursor-based pagination:

| Parameter  | Type | Default | Description               |
| ---------- | ---- | ------- | ------------------------- |
| `page`     | int  | 1       | Page number (1-indexed)   |
| `pageSize` | int  | 25      | Items per page (max: 100) |

**Response headers:**

| Header           | Description                    |
| ---------------- | ------------------------------ |
| `X-Total-Count`  | Total items matching the query |
| `X-Total-Pages`  | Total pages available          |
| `X-Current-Page` | Current page number            |

### 13.5 Date & Time Conventions

| Type             | Format                    | Example                       |
| ---------------- | ------------------------- | ----------------------------- |
| `DateTimeOffset` | ISO 8601 with timezone    | `"2026-03-20T10:00:00+08:00"` |
| `DateOnly`       | ISO 8601 date             | `"2026-03-20"`                |
| `TimeOnly`       | 24-hour time with seconds | `"09:30:00"`                  |

All date/time values in the API are in the **tenant's configured timezone** (default: `Asia/Manila`, UTC+8).

### 13.6 Idempotency

For operations that create resources (bookings, webhooks, staff), the system uses natural keys for deduplication:

| Resource     | Natural Key                                       | Behavior on Conflict   |
| ------------ | ------------------------------------------------- | ---------------------- |
| Booking      | `bookingTypeSlug` + `startTime` + `customerEmail` | Returns `409 Conflict` |
| Booking Type | `slug`                                            | Returns `409 Conflict` |
| Staff Member | `email` (within tenant)                           | Returns `409 Conflict` |
| Webhook      | `bookingTypeSlug` + `url`                         | Returns `409 Conflict` |

For payment operations, the payment provider's transaction ID serves as the idempotency key.

### 13.7 Health Checks

```
GET /health          — Basic liveness check (200 OK)
GET /health/ready    — Readiness check (database, Redis, external services)
```

**Readiness response:**

```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "redis": "Healthy",
    "smtp": "Healthy"
  }
}
```

### 13.8 Versioning

The API is currently at **v1.0** and does not use URL-based versioning. Breaking changes will be introduced via a `/v2/` prefix in the future, with the v1 API maintained for at least 12 months after v2 launch.

### 13.9 Environment URLs

| Environment | Base URL                          |
| ----------- | --------------------------------- |
| Production  | `https://api.chronith.io`         |
| Staging     | `https://staging-api.chronith.io` |
| Local Dev   | `http://localhost:5001`           |
