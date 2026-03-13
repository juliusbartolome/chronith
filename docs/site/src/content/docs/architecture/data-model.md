---
title: Data Model
description: Chronith's core entities, relationships, and storage conventions.
---

## Entity Relationship Diagram

```
Tenant
  ├── BookingType (TimeSlot | Calendar)
  │     ├── AvailabilityWindow[]
  │     └── CustomField[]
  ├── StaffMember
  │     └── StaffAvailabilityWindow[]
  ├── Booking
  │     ├── BookingType (ref)
  │     ├── StaffMember? (ref)
  │     └── RecurringSeries? (ref)
  ├── RecurringSeries
  │     └── Booking[]
  ├── Webhook
  │     └── WebhookDelivery[]
  ├── NotificationChannel (Email | SMS | Push)
  ├── NotificationTemplate[]
  ├── AuditLog[]
  └── ApiKey[]
```

## Core entities

| Entity | Key Fields |
|--------|-----------|
| `Tenant` | `Id`, `BusinessName`, `Slug`, `Plan` |
| `BookingType` | `Id`, `TenantId`, `Title`, `Kind`, `DurationMinutes`, `PriceCentavos` |
| `StaffMember` | `Id`, `TenantId`, `Name`, `Email`, `IsActive` |
| `Booking` | `Id`, `TenantId`, `BookingTypeId`, `Status`, `StartTime`, `EndTime`, `CustomerEmail` |
| `RecurringSeries` | `Id`, `TenantId`, `Rrule`, `StartTime` |
| `Webhook` | `Id`, `TenantId`, `Url`, `Events`, `IsActive` |
| `AuditLog` | `Id`, `TenantId`, `ActorId`, `Action`, `ResourceType`, `ResourceId`, `OccurredAt` |

## Storage conventions

- **Schema:** `chronith` (PostgreSQL)
- **Table names:** `snake_case` (e.g., `booking_types`, `staff_members`)
- **Primary keys:** `Guid` (UUID v4)
- **Soft deletes:** All entities have `IsDeleted bool`. Hard deletes are never used.
- **Enums:** Stored as strings for readability and migration safety
- **Optimistic concurrency:** `xmin` system column (PostgreSQL row version)

## Currency

All prices are stored as `long` in **centavos** (PHP). There is no decimal arithmetic in the domain.

| Amount (PHP) | Stored as (centavos) |
|-------------|---------------------|
| ₱100.00 | 10000 |
| ₱1,500.00 | 150000 |
| ₱0.00 (free) | 0 |

Free bookings (`PriceCentavos == 0`) skip the `PendingPayment` state.
