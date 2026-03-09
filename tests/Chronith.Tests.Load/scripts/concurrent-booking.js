/**
 * concurrent-booking.js — Concurrency conflict test
 *
 * 50 VUs all racing to book the exact same time slot on a booking type
 * with capacity=1.
 *
 * Expected outcome:
 *   - Exactly 1 request returns 201 (booking created)
 *   - All others return 409 (conflict) or 400 (validation)
 *   - The `booking_conflicts` custom counter tracks rejected bookings
 *
 * Prerequisites:
 *   - Running Chronith API (default: http://localhost:5000)
 *   - Booking type with slug "capacity-one-type" seeded with capacity=1
 *   - The slot 2026-05-04T10:00:00Z–11:00:00Z must be within availability windows
 *
 * Run:
 *   k6 run tests/Chronith.Tests.Load/scripts/concurrent-booking.js
 */
import http from "k6/http";
import { check } from "k6";
import { Counter } from "k6/metrics";
import { authHeader, baseUrl } from "./helpers.js";

export const options = {
  vus: 50,
  duration: "10s",
  thresholds: {
    // At most 1 VU should succeed across the entire run; the rest should get conflicts
    booking_successes: ["count<=1"],
  },
};

const booking_conflicts = new Counter("booking_conflicts");
const booking_successes = new Counter("booking_successes");

const SLUG = __ENV.BOOKING_TYPE_SLUG || "capacity-one-type";
const SLOT_START = __ENV.SLOT_START || "2026-05-04T10:00:00Z";
const SLOT_END = __ENV.SLOT_END || "2026-05-04T11:00:00Z";

export default function () {
  const url = `${baseUrl()}/v1/booking-types/${SLUG}/bookings`;
  const payload = JSON.stringify({
    startTime: SLOT_START,
    customerEmail: `concurrent-${__VU}@example.com`,
  });

  const params = {
    headers: {
      ...authHeader("Customer"),
      "Content-Type": "application/json",
    },
  };

  const res = http.post(url, payload, params);

  const created = res.status === 201;
  const conflict = res.status === 409 || res.status === 400;

  check(res, {
    "result is 201, 400, or 409": (r) =>
      r.status === 201 || r.status === 409 || r.status === 400,
  });

  if (created) booking_successes.add(1);
  if (conflict) booking_conflicts.add(1);
}
