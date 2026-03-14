# Changelog

All notable changes to Chronith are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [v1.0.0] — 2026-03-12

### Added

- Complete admin dashboard: analytics (Recharts, 3 tabs), notifications management, webhook management, customer management, audit log viewer, recurring bookings
- Tenant branding (TenantSettings): logo, primary color, custom domain, welcome message
- Public customer-facing booking flow: multi-step (select type → pick date/time → staff → details → confirm → success)
- Customer auth in public context: registration, login, booking history, cancel/reschedule
- Tenant self-service onboarding: signup wizard (3 steps), post-signup guided wizard (5 steps)
- Subscription management: TenantPlan and TenantSubscription domain models, EF Core persistence, CQRS, FastEndpoints
- Plan enforcement: MediatR `PlanEnforcementBehavior` blocks resource-creation when plan limits are exceeded
- Plan limit provider: dashboard context that intercepts 403 errors and shows upgrade dialog
- Settings > Subscription page: usage meters with color thresholds (yellow ≥ 80%, red ≥ 100%)
- C# SDK (`Chronith.Client`): strongly-typed .NET client library with DI integration, retry policy, IHttpClientFactory
- Starlight documentation site with full API reference, guides, and architecture docs
- Docs CI workflow (GitHub Actions) for automated docs build and deploy
- Security audit: OWASP Top 10 review, dependency scan, penetration test checklist
- Playwright E2E tests: all major dashboard and public booking flows on desktop + mobile viewports
- k6 load tests: customer auth flow, analytics queries, public booking flow, dashboard simulation

---

## [v0.9.0] — 2026-02-xx

### Added

- TypeScript SDK (`@chronith/sdk`): auto-generated from OpenAPI spec, TanStack Query hooks
- Admin dashboard (core): Next.js 15, Tailwind CSS 4, shadcn/ui canary
- Pages: Dashboard, Bookings, Booking Types, Staff, Availability, Waitlist, Time Blocks, Settings
- CSV/PDF data export
- Dashboard CI workflow

---

## [v0.8.0] — 2026-01-xx

### Added

- Audit logging: `AuditBehavior` captures all admin mutations, stored in PostgreSQL
- OpenTelemetry: traces + metrics exported via OTLP
- Security hardening: CSP headers, rate limiting enhancements, CORS allowlist
- Database optimization: composite indexes, covering indexes, BRIN index on timestamps
- Notification templates: configurable via database, per-tenant, per-channel, per-event

---

## [v0.7.0] — 2025-12-xx

### Added

- API versioning (v1 prefix on all endpoints)
- Customer accounts: built-in auth (email/password) + OIDC provider integration
- Recurring bookings: recurrence rules, series management, occurrence generation
- Idempotency keys on mutating endpoints (Redis-backed, 24-hour window)
- Booking lookup by external reference

---

## [v0.6.0] — 2025-11-xx

### Added

- Staff management: CRUD, availability windows, staff assignment to bookings
- Booking lifecycle enhancements: reschedule, waitlist (offer/accept/decline), time blocks
- Custom fields: schema definition on booking types, validation on booking creation
- Notifications: MailKit (SMTP), Twilio (SMS), FirebaseAdmin (push) channels
- Analytics API: booking stats, revenue stats, utilization stats, KPI cards
- Public booking endpoints (read-only): booking types, availability, create booking without admin auth
- iCal feed: `.ics` download per booking

---

## [v0.5.0] — 2025-10-xx

### Added

- Payment integration: `IPaymentProvider` abstraction
- PayMongo provider: payment intent creation, webhook handling
- Free booking flow: bookings with price = 0 skip `PendingPayment` → go directly to `PendingVerification`
- Booking price calculation: price from booking type, custom overrides
- Payment webhook receiver

---

## [v0.4.0] — 2025-09-xx

### Added

- JWT authentication (HMAC-SHA256, asymmetric key option)
- API key authentication (`X-Api-Key` header)
- Role-based access control: `PlatformAdmin`, `TenantAdmin`, `Staff`, `Customer`
- Redis caching: availability slots (5-minute TTL), tenant settings (10-minute TTL)
- Customer callbacks: `POST /v1/bookings/{id}/callback` for partner integrations
- Rate limiting: IP-based and tenant-based limits on auth and public endpoints

---

## [v0.3.0] — 2025-08-xx

### Added

- Optimistic concurrency: PostgreSQL `xmin` row version on all entities
- Health checks: liveness and readiness endpoints (`/health/live`, `/health/ready`)
- Integration test infrastructure: Testcontainers (PostgreSQL + Redis)
- Functional test infrastructure: `FunctionalTestFixture`, `SeedData`, `TestJwtFactory`
- CI pipeline: GitHub Actions with Postgres + Redis service containers

---

## [v0.2.0] — 2025-07-xx

### Added

- Availability engine: sliding window calculation, slot generation
- Slot conflict detection: `SlotConflictException` on overlapping bookings
- Webhook outbox: reliable delivery with retry, exponential backoff
- Booking status notifications: fires MediatR notifications on status changes
- `BookingStatus` state machine: `PendingPayment → PendingVerification → Confirmed ↔ Cancelled`

---

## [v0.1.0] — 2025-06-xx

### Added

- Core domain models: `Tenant`, `BookingType` (abstract, `TimeSlotBookingType` / `CalendarBookingType`), `Booking`, `StaffMember`, `AvailabilityWindow`
- Clean architecture: Domain ← Application ← Infrastructure ← API (strict layer rules)
- CQRS via MediatR: commands, queries, handlers, validators
- EF Core + Npgsql: PostgreSQL 17, entity configurations, global query filters for tenant isolation + soft delete
- Multi-tenancy: all data scoped by `TenantId`, global query filters on all entities
- Basic booking CRUD: create, get, list, cancel
- Docker multi-stage build, Docker Compose stack
- xUnit unit tests, FluentAssertions, NSubstitute
