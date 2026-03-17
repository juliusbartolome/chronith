/**
 * booking-lifecycle.js — Full booking lifecycle load test
 *
 * 20 VUs for 30 seconds.
 * Each VU performs the full flow: create → pay → confirm → get.
 * Threshold: p95 end-to-end per-step request duration < 500 ms.
 *
 * Prerequisites:
 *   - Running Chronith API (default: http://localhost:5000)
 *   - Booking type with slug "test-type" seeded with sufficient capacity
 *     and availability covering 2026-06-01 (Monday)
 *
 * Run:
 *   k6 run tests/Chronith.Tests.Load/scripts/booking-lifecycle.js
 */
import http from "k6/http";
import { check, sleep } from "k6";
import { authHeader, baseUrl } from "./helpers.js";

export const options = {
  vus: 20,
  duration: "30s",
  thresholds: {
    http_req_duration: ["p(95)<500"],
    // http_req_failed is not used here — create steps intentionally return 409 once
    // a slot's capacity is exhausted; the script handles this by skipping the remaining
    // lifecycle steps. Only the duration threshold enforces performance correctness.
  },
};

const SLUG = __ENV.BOOKING_TYPE_SLUG || "test-type";

// 10 slots across multiple dates so concurrent VUs don't collide
// Each slot is 08:00–09:00 on a different date (Mon 2026-06-01 through 2026-06-10)
const SLOTS = Array.from({ length: 20 }, (_, i) => {
  const day = 1 + (i % 10);
  const hour = 8 + Math.floor(i / 10);
  const pad = (n) => String(n).padStart(2, "0");
  return {
    start: `2026-06-${pad(day)}T${pad(hour)}:00:00Z`,
    end: `2026-06-${pad(day)}T${pad(hour + 1)}:00:00Z`,
  };
});

export default function () {
  const slot = SLOTS[(__VU - 1) % SLOTS.length];
  const base = baseUrl();

  // ── Step 1: Create booking (Customer role) ──────────────────────────────
  const createRes = http.post(
    `${base}/v1/booking-types/${SLUG}/bookings`,
    JSON.stringify({
      startTime: slot.start,
      customerEmail: `lifecycle-${__VU}-${__ITER}@example.com`,
    }),
    {
      headers: {
        ...authHeader("Customer"),
        "Content-Type": "application/json",
      },
    },
  );

  const created = check(createRes, {
    "create: status 201": (r) => r.status === 201,
  });

  if (!created) {
    // Slot already taken for this VU; skip the rest of the flow
    sleep(0.5);
    return;
  }

  const bookingId = JSON.parse(createRes.body).id;

  // ── Step 2: Pay booking (Payment service role) ────────────────────────
  const payRes = http.post(
    `${base}/v1/bookings/${bookingId}/pay`,
    JSON.stringify({ bookingTypeSlug: SLUG }),
    {
      headers: {
        ...authHeader("TenantPaymentService"),
        "Content-Type": "application/json",
      },
    },
  );

  check(payRes, {
    "pay: status 200": (r) => r.status === 200,
  });

  // ── Step 3: Confirm booking (Staff role) ───────────────────────────────
  const confirmRes = http.post(
    `${base}/v1/bookings/${bookingId}/confirm`,
    JSON.stringify({ bookingTypeSlug: SLUG }),
    {
      headers: {
        ...authHeader("TenantStaff"),
        "Content-Type": "application/json",
      },
    },
  );

  check(confirmRes, {
    "confirm: status 200": (r) => r.status === 200,
  });

  // ── Step 4: Get booking (Admin role) ──────────────────────────────────
  const getRes = http.get(`${base}/v1/bookings/${bookingId}`, {
    headers: authHeader("TenantAdmin"),
  });

  check(getRes, {
    "get: status 200": (r) => r.status === 200,
    "get: status is Confirmed": (r) => {
      const body = JSON.parse(r.body);
      // BookingStatus enum is serialized as a string via JsonStringEnumConverter
      return body.status === "Confirmed";
    },
  });

  sleep(0.3);
}
