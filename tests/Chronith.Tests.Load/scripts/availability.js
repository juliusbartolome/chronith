/**
 * availability.js — Load test for GET /booking-types/{slug}/availability
 *
 * 100 VUs for 60 seconds.
 * Threshold: p95 response time < 100 ms.
 *
 * Prerequisites:
 *   - Running Chronith API (default: http://localhost:5000)
 *   - Booking type with slug "test-type" seeded in the database
 *
 * Run:
 *   k6 run tests/Chronith.Tests.Load/scripts/availability.js
 *   k6 run -e BASE_URL=http://staging:5000 tests/Chronith.Tests.Load/scripts/availability.js
 */
import http from "k6/http";
import { check, sleep } from "k6";
import { authHeader, baseUrl } from "./helpers.js";

export const options = {
  vus: 100,
  duration: "60s",
  thresholds: {
    http_req_duration: ["p(95)<100"],
    http_req_failed: ["rate<0.01"],
  },
};

const SLUG = __ENV.BOOKING_TYPE_SLUG || "test-type";

export default function () {
  const from = "2026-04-01T00:00:00Z";
  const to = "2026-04-08T00:00:00Z";
  const url = `${baseUrl()}/booking-types/${SLUG}/availability?from=${from}&to=${to}`;

  const res = http.get(url, { headers: authHeader("Customer") });

  check(res, {
    "status is 200": (r) => r.status === 200,
    "has slots field": (r) => {
      const body = JSON.parse(r.body);
      return Array.isArray(body.slots);
    },
  });

  sleep(0.1);
}
