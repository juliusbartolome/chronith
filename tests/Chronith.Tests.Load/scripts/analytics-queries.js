import http from "k6/http";
import { check, sleep } from "k6";
import { Rate, Trend } from "k6/metrics";

const BASE_URL = __ENV.BASE_URL || "http://localhost:5001";
const JWT_SIGNING_KEY =
  __ENV.JWT_SIGNING_KEY || "change-me-in-production-at-least-32-chars";
const ADMIN_TOKEN = __ENV.ADMIN_TOKEN || "";

const analyticsErrors = new Rate("analytics_errors");
const analyticsLatency = new Trend("analytics_latency_ms");

export const options = {
  stages: [
    { duration: "30s", target: 20 },
    { duration: "3m", target: 100 },
    { duration: "1m", target: 100 },
    { duration: "30s", target: 0 },
  ],
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<2000"],
    analytics_errors: ["rate<0.02"],
    analytics_latency_ms: ["p(95)<1500"],
  },
};

export default function () {
  const headers = { Authorization: `Bearer ${ADMIN_TOKEN}` };

  const now = new Date();
  const thirtyDaysAgo = new Date(now);
  thirtyDaysAgo.setDate(now.getDate() - 30);

  const from = thirtyDaysAgo.toISOString().split("T")[0];
  const to = now.toISOString().split("T")[0];

  // Booking analytics
  const start = Date.now();
  const bookingRes = http.get(
    `${BASE_URL}/v1/analytics/bookings?from=${from}&to=${to}&groupBy=Day`,
    { headers },
  );
  analyticsLatency.add(Date.now() - start);
  analyticsErrors.add(bookingRes.status !== 200);
  check(bookingRes, { "booking analytics 200": (r) => r.status === 200 });

  sleep(0.2);

  // Revenue analytics
  const revenueRes = http.get(
    `${BASE_URL}/v1/analytics/revenue?from=${from}&to=${to}&groupBy=Week`,
    { headers },
  );
  analyticsErrors.add(revenueRes.status !== 200);
  check(revenueRes, { "revenue analytics 200": (r) => r.status === 200 });

  sleep(0.2);

  // Utilization analytics
  const utilRes = http.get(
    `${BASE_URL}/v1/analytics/utilization?from=${from}&to=${to}`,
    { headers },
  );
  check(utilRes, { "utilization analytics 200": (r) => r.status === 200 });

  sleep(1);
}
