import http from "k6/http";
import { check, sleep, group } from "k6";
import { Rate } from "k6/metrics";

const BASE_URL = __ENV.BASE_URL || "http://localhost:5001";
const ADMIN_TOKEN = __ENV.ADMIN_TOKEN || "";

const dashboardErrors = new Rate("dashboard_errors");

export const options = {
  stages: [
    { duration: "30s", target: 15 },
    { duration: "3m", target: 75 },
    { duration: "2m", target: 75 },
    { duration: "30s", target: 0 },
  ],
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<1500"],
    dashboard_errors: ["rate<0.02"],
  },
};

export default function () {
  const headers = { Authorization: `Bearer ${ADMIN_TOKEN}` };

  group("Dashboard home", function () {
    const r = http.get(`${BASE_URL}/v1/bookings?page=1&pageSize=20`, {
      headers,
    });
    dashboardErrors.add(r.status !== 200);
    check(r, { "bookings list 200": (r) => r.status === 200 });
    sleep(0.3);
  });

  group("Analytics overview", function () {
    const now = new Date();
    const weekAgo = new Date(now);
    weekAgo.setDate(now.getDate() - 7);
    const from = weekAgo.toISOString().split("T")[0];
    const to = now.toISOString().split("T")[0];

    const r = http.get(
      `${BASE_URL}/v1/analytics/bookings?from=${from}&to=${to}&groupBy=Day`,
      { headers },
    );
    dashboardErrors.add(r.status !== 200);
    check(r, { "analytics 200": (r) => r.status === 200 });
    sleep(0.3);
  });

  group("Staff list", function () {
    const r = http.get(`${BASE_URL}/v1/staff?page=1&pageSize=20`, { headers });
    dashboardErrors.add(r.status !== 200);
    check(r, { "staff 200": (r) => r.status === 200 });
    sleep(0.3);
  });

  group("Usage check", function () {
    const r = http.get(`${BASE_URL}/v1/tenant/usage`, { headers });
    dashboardErrors.add(r.status !== 200);
    check(r, { "usage 200": (r) => r.status === 200 });
    sleep(0.2);
  });

  group("Subscription", function () {
    const r = http.get(`${BASE_URL}/v1/tenant/subscription`, { headers });
    dashboardErrors.add(r.status !== 200);
    check(r, { "subscription 200": (r) => r.status === 200 });
    sleep(0.2);
  });

  sleep(1);
}
