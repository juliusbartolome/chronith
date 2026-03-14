import http from "k6/http";
import { check, sleep } from "k6";
import { Rate, Trend } from "k6/metrics";

const BASE_URL = __ENV.BASE_URL || "http://localhost:5001";
const TENANT_SLUG = __ENV.TENANT_SLUG || "load-test-tenant";
const BOOKING_TYPE_SLUG = __ENV.BOOKING_TYPE_SLUG || "consultation";

const bookingErrors = new Rate("booking_errors");
const availabilityLatency = new Trend("availability_latency_ms");
const bookingCreationLatency = new Trend("booking_creation_latency_ms");

export const options = {
  stages: [
    { duration: "30s", target: 20 },
    { duration: "2m", target: 100 },
    { duration: "2m", target: 100 },
    { duration: "30s", target: 0 },
  ],
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<1000"],
    booking_errors: ["rate<0.05"],
    availability_latency_ms: ["p(95)<500"],
    booking_creation_latency_ms: ["p(95)<800"],
  },
};

function randomEmail() {
  return `booker-${Date.now()}-${Math.random().toString(36).slice(2)}@test.com`;
}

function getDateInDays(daysAhead) {
  const d = new Date();
  d.setDate(d.getDate() + daysAhead);
  return d.toISOString().split("T")[0];
}

export default function () {
  const tenantSlug = TENANT_SLUG;

  // Step 1: Get tenant public info (branding)
  const brandingRes = http.get(`${BASE_URL}/v1/public/${tenantSlug}/settings`);
  check(brandingRes, { "branding 200": (r) => r.status === 200 });

  sleep(0.5);

  // Step 2: Get booking types
  const btRes = http.get(`${BASE_URL}/v1/public/${tenantSlug}/booking-types`);
  check(btRes, { "booking types 200": (r) => r.status === 200 });

  sleep(0.5);

  // Step 3: Get availability
  const date = getDateInDays(3);
  const start = Date.now();
  const availRes = http.get(
    `${BASE_URL}/v1/public/${tenantSlug}/booking-types/${BOOKING_TYPE_SLUG}/availability?date=${date}`,
  );
  availabilityLatency.add(Date.now() - start);
  check(availRes, { "availability 200": (r) => r.status === 200 });

  if (availRes.status !== 200) {
    sleep(1);
    return;
  }

  let slots;
  try {
    slots = JSON.parse(availRes.body).slots ?? [];
  } catch {
    sleep(1);
    return;
  }

  if (slots.length === 0) {
    sleep(1);
    return;
  }

  const slot = slots[0];

  // Step 4: Create booking (guest)
  const bookingStart = Date.now();
  const bookingRes = http.post(
    `${BASE_URL}/v1/public/${tenantSlug}/bookings`,
    JSON.stringify({
      bookingTypeSlug: BOOKING_TYPE_SLUG,
      date,
      startTime: slot.startTime,
      customerEmail: randomEmail(),
      customerName: "Load Test User",
    }),
    { headers: { "Content-Type": "application/json" } },
  );
  bookingCreationLatency.add(Date.now() - bookingStart);
  bookingErrors.add(bookingRes.status !== 201 && bookingRes.status !== 200);
  check(bookingRes, {
    "booking created": (r) => r.status === 201 || r.status === 200,
  });

  sleep(1);
}
