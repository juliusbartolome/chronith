# Chronith k6 Load Tests

Load test scripts for the Chronith booking API. All scripts require a **running Chronith API** instance and pre-seeded data.

## Prerequisites

- [k6](https://grafana.com/docs/k6/latest/set-up/install-k6/) installed (`brew install k6` on macOS)
- A running Chronith API (see [Containerization](#running-with-docker))
- Seed data (see [Seed Data Requirements](#seed-data-requirements))

## Running with Docker

```bash
docker compose up --build
```

The API will be available at `http://localhost:5000`.

## Scripts

### `availability.js` — Read-heavy availability queries

```bash
k6 run tests/Chronith.Tests.Load/scripts/availability.js
# Override base URL:
k6 run -e BASE_URL=http://staging:5000 tests/Chronith.Tests.Load/scripts/availability.js
```

- **100 VUs / 60 seconds**
- Hits `GET /booking-types/{slug}/availability` with a Customer token
- **Threshold:** `p95 < 100 ms`

### `create-booking.js` — Booking creation throughput

```bash
k6 run tests/Chronith.Tests.Load/scripts/create-booking.js
```

- **50 VUs / 30 seconds**
- Each VU posts unique bookings spread across different slots to minimise conflicts
- **Threshold:** `p95 < 200 ms`

### `concurrent-booking.js` — Concurrency conflict detection

```bash
k6 run tests/Chronith.Tests.Load/scripts/concurrent-booking.js
# Override slot times:
k6 run -e SLOT_START=2026-07-06T10:00:00Z -e SLOT_END=2026-07-06T11:00:00Z \
  tests/Chronith.Tests.Load/scripts/concurrent-booking.js
```

- **50 VUs** all racing to book the **same slot** on a capacity-1 booking type
- Expected: exactly **1 success (201)**, all others **409 or 400**
- Tracks a custom `booking_conflicts` counter
- **Requires:** booking type with slug `capacity-one-type` and `capacity = 1`

### `booking-lifecycle.js` — Full lifecycle throughput

```bash
k6 run tests/Chronith.Tests.Load/scripts/booking-lifecycle.js
```

- **20 VUs / 30 seconds**
- Each VU runs: `POST /bookings` (Customer) → `POST /bookings/{id}/confirm` (Staff) → `GET /bookings/{id}` (Admin)
- **Threshold:** `p95 < 500 ms`

## Environment Variables

| Variable            | Default                                     | Description                          |
| ------------------- | ------------------------------------------- | ------------------------------------ |
| `BASE_URL`          | `http://localhost:5000`                     | Chronith API base URL                |
| `JWT_SIGNING_KEY`   | `load-test-signing-key-at-least-32-chars!!` | HS256 key (must match server config) |
| `BOOKING_TYPE_SLUG` | `test-type` / `capacity-one-type`           | Booking type slug (per script)       |
| `SLOT_START`        | `2026-05-04T10:00:00Z`                      | concurrent-booking only              |
| `SLOT_END`          | `2026-05-04T11:00:00Z`                      | concurrent-booking only              |

## Seed Data Requirements

| Script                  | Required booking type slug | Capacity | Availability                           |
| ----------------------- | -------------------------- | -------- | -------------------------------------- |
| `availability.js`       | `test-type`                | ≥ 1      | Covers 2026-04-01–2026-04-08           |
| `create-booking.js`     | `test-type`                | ≥ 50     | Covers 2026-04-07 and 2026-04-14, 21   |
| `concurrent-booking.js` | `capacity-one-type`        | **1**    | Covers `SLOT_START` day                |
| `booking-lifecycle.js`  | `test-type`                | ≥ 20     | Covers 2026-06-01 (Monday) 08:00–18:00 |

A booking type can be created via `POST /booking-types` with a TenantAdmin token.

## Expected Thresholds

| Script                  | Threshold                        |
| ----------------------- | -------------------------------- |
| `availability.js`       | `http_req_duration[p95] < 100ms` |
| `create-booking.js`     | `http_req_duration[p95] < 200ms` |
| `concurrent-booking.js` | `booking_successes count ≤ 1`    |
| `booking-lifecycle.js`  | `http_req_duration[p95] < 500ms` |

## Running with Custom Auth Key

The server must be configured with a matching signing key:

```bash
# Docker Compose
JWT__SIGNING_KEY=my-custom-32-char-key-here docker compose up

# k6
k6 run -e JWT_SIGNING_KEY=my-custom-32-char-key-here \
  tests/Chronith.Tests.Load/scripts/availability.js
```
