/**
 * create-booking.js — Load test for POST /booking-types/{slug}/bookings
 *
 * 50 VUs for 30 seconds, each VU posting unique bookings.
 * Threshold: p95 response time < 200 ms.
 *
 * Prerequisites:
 *   - Running Chronith API (default: http://localhost:5000)
 *   - Booking type with slug "test-type" seeded with sufficient capacity
 *   - The booking type's availability windows must cover 2026-04-07 (Monday)
 *
 * Run:
 *   k6 run tests/Chronith.Tests.Load/scripts/create-booking.js
 */
import http from "k6/http";
import { check, sleep } from "k6";
import { authHeader, baseUrl } from "./helpers.js";

export const options = {
  vus: 50,
  duration: "30s",
  thresholds: {
    http_req_duration: ["p(95)<200"],
    http_req_failed: ["rate<0.05"],
  },
};

const SLUG = __ENV.BOOKING_TYPE_SLUG || "test-type";

// Spread bookings across many different slots on different days so they don't conflict
const BASE_SLOTS = [
  { start: "2026-04-07T08:00:00Z", end: "2026-04-07T09:00:00Z" },
  { start: "2026-04-07T09:00:00Z", end: "2026-04-07T10:00:00Z" },
  { start: "2026-04-07T10:00:00Z", end: "2026-04-07T11:00:00Z" },
  { start: "2026-04-07T11:00:00Z", end: "2026-04-07T12:00:00Z" },
  { start: "2026-04-07T13:00:00Z", end: "2026-04-07T14:00:00Z" },
  { start: "2026-04-07T14:00:00Z", end: "2026-04-07T15:00:00Z" },
  { start: "2026-04-07T15:00:00Z", end: "2026-04-07T16:00:00Z" },
  { start: "2026-04-07T16:00:00Z", end: "2026-04-07T17:00:00Z" },
  { start: "2026-04-14T08:00:00Z", end: "2026-04-14T09:00:00Z" },
  { start: "2026-04-14T09:00:00Z", end: "2026-04-14T10:00:00Z" },
  { start: "2026-04-14T10:00:00Z", end: "2026-04-14T11:00:00Z" },
  { start: "2026-04-14T11:00:00Z", end: "2026-04-14T12:00:00Z" },
  { start: "2026-04-21T08:00:00Z", end: "2026-04-21T09:00:00Z" },
  { start: "2026-04-21T09:00:00Z", end: "2026-04-21T10:00:00Z" },
  { start: "2026-04-21T10:00:00Z", end: "2026-04-21T11:00:00Z" },
];

export default function () {
  // Each VU+iteration picks a different slot index to minimise conflicts
  const slotIndex = (__VU * 100 + __ITER) % BASE_SLOTS.length;
  const slot = BASE_SLOTS[slotIndex];

  const url = `${baseUrl()}/booking-types/${SLUG}/bookings`;
  const payload = JSON.stringify({
    start: slot.start,
    end: slot.end,
    customerEmail: `k6-${__VU}-${__ITER}@example.com`,
    customerName: `VU ${__VU} Iter ${__ITER}`,
  });

  const params = {
    headers: {
      ...authHeader("Customer"),
      "Content-Type": "application/json",
    },
  };

  const res = http.post(url, payload, params);

  check(res, {
    "booking created (201) or conflict (409/400)": (r) =>
      r.status === 201 || r.status === 409 || r.status === 400,
  });

  sleep(0.2);
}
