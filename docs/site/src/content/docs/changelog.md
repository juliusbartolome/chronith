---
title: Changelog
description: Version history for Chronith.
---

All notable changes to Chronith are documented here.

## v1.0.0 — Production Release

- Full production readiness: hardened security, observability, and compliance
- Starlight documentation site (`docs/site/`)
- TypeScript and C# SDKs published to npm and NuGet
- Kubernetes deployment guide
- iCal feed for public calendars
- Enterprise plan support

## v0.9.0

- Multi-region deployment support
- Advanced recurring bookings (RRULE full support)
- Webhook delivery guaranteed with dead-letter queue
- Customer OIDC login (Google, Microsoft)
- PDF report exports

## v0.8.0

- Audit logging with plan-based retention
- OpenTelemetry traces and metrics (OTLP export)
- Security hardening (AES-256-GCM encryption for notification credentials)
- Database index optimization
- Notification templates with variable interpolation

## v0.6.0

- Staff management (create, assign availability windows, deactivate)
- Booking lifecycle enhancements: reschedule, waitlist, time blocks
- Custom fields on booking types
- Email (MailKit), SMS (Twilio), Push (Firebase) notifications
- Analytics endpoints (bookings, revenue, utilization)
- Public booking endpoints and iCal feed

## v0.5.0

- Payment integration with pluggable `IPaymentProvider`
- PayMongo provider for Philippine payments
- Free booking flow (skip `PendingPayment`)
- Price stored as `long` in centavos (PHP)

## v0.4.0

- JWT (HMAC-SHA256) authentication
- API key authentication (`X-Api-Key`)
- Redis caching for availability and analytics
- Customer account callbacks
- Rate limiting (fixed window)

## v0.3.0

- Webhook outbox pattern for reliable delivery
- Booking status change notifications
- Exponential backoff retry (5 attempts)

## v0.2.0

- Availability engine with configurable windows
- Slot conflict detection (`SlotConflictException`)
- Calendar booking type support

## v0.1.0

- Initial release
- Core domain models: `Booking`, `BookingType`, `Tenant`, `StaffMember`
- Booking CRUD
- Tenant isolation via global query filters
- FastEndpoints API framework
- EF Core + PostgreSQL persistence
- Docker Compose stack
