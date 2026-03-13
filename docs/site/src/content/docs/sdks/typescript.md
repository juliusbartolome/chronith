---
title: TypeScript SDK
description: Official TypeScript/JavaScript SDK for the Chronith API.
---

## Installation

```sh
npm install @chronith/sdk
```

## Quick start

```ts
import { ChronithClient } from "@chronith/sdk";

const client = new ChronithClient({
  baseUrl: "https://api.yourdomain.com",
  apiKey: "chron_live_xxxxxxxxxxxxxxxx",
});

// List booking types
const bookingTypes = await client.bookingTypes.list();

// Create a booking
const booking = await client.bookings.create({
  bookingTypeId: "3fa85f64-...",
  startTime: "2026-04-15T09:00:00Z",
  customerName: "Jane Doe",
  customerEmail: "jane@example.com",
});

console.log(booking.id);
```

## Authentication

The SDK supports both API key and JWT authentication:

```ts
// API key
const client = new ChronithClient({
  baseUrl: "https://api.yourdomain.com",
  apiKey: "chron_live_xxxxxxxxxxxxxxxx",
});

// JWT token
const client = new ChronithClient({
  baseUrl: "https://api.yourdomain.com",
  jwtToken: "eyJhbGciOiJIUzI1NiIs...",
});
```

## Error handling

```ts
import { ChronithApiError } from "@chronith/sdk";

try {
  const booking = await client.bookings.get("nonexistent-id");
} catch (error) {
  if (error instanceof ChronithApiError) {
    console.error(`Status: ${error.statusCode}`);
    console.error(`Title: ${error.title}`);
    console.error(`Detail: ${error.detail}`);
  }
}
```

## Available services

| Service | Description |
|---------|-------------|
| `client.bookings` | Create, get, confirm, cancel, reschedule bookings |
| `client.bookingTypes` | Manage booking types |
| `client.availability` | Query available slots |
| `client.staff` | Manage staff members |
| `client.analytics` | Query analytics data |
| `client.webhooks` | Manage webhooks |
| `client.tenant` | Tenant settings and subscriptions |
| `client.recurring` | Create and manage recurring series |
| `client.audit` | Query audit logs |
| `client.notifications` | Configure notification channels |
