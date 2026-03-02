/**
 * booking-lifecycle.js — Full booking lifecycle load test
 *
 * 20 VUs for 30 seconds.
 * Each VU performs the full flow: create booking (Customer) → confirm (Staff) → get (Admin).
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
    http_req_failed: ["rate<0.05"],
  },
};

const SLUG = __ENV.BOOKING_TYPE_SLUG || "test-type";

// 20 slots spread across the day so concurrent VUs don't collide
const SLOTS = Array.from({ length: 20 }, (_, i) => {
  const hour = 8 + i;
  const pad = (n) => String(n).padStart(2, "0");
  return {
    start: `2026-06-01T${pad(hour)}:00:00Z`,
    end: `2026-06-01T${pad(hour + 1)}:00:00Z`,
  };
});

export default function () {
  const slot = SLOTS[(__VU - 1) % SLOTS.length];
  const base = baseUrl();

  // ── Step 1: Create booking (Customer role) ──────────────────────────────
  const createRes = http.post(
    `${base}/booking-types/${SLUG}/bookings`,
    JSON.stringify({
      start: slot.start,
      end: slot.end,
      customerEmail: `lifecycle-${__VU}-${__ITER}@example.com`,
      customerName: `Lifecycle VU ${__VU}`,
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

  // ── Step 2: Confirm booking (Staff role) ───────────────────────────────
  const confirmRes = http.post(`${base}/bookings/${bookingId}/confirm`, null, {
    headers: authHeader("TenantStaff"),
  });

  check(confirmRes, {
    "confirm: status 200": (r) => r.status === 200,
  });

  // ── Step 3: Get booking (Admin role) ──────────────────────────────────
  const getRes = http.get(`${base}/bookings/${bookingId}`, {
    headers: authHeader("TenantAdmin"),
  });

  check(getRes, {
    "get: status 200": (r) => r.status === 200,
    "get: status is Confirmed": (r) => {
      const body = JSON.parse(r.body);
      return body.status === "Confirmed";
    },
  });

  sleep(0.3);
}
