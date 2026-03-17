import http from "k6/http";
import { check, sleep } from "k6";
import { Rate, Trend } from "k6/metrics";

const BASE_URL = __ENV.BASE_URL || "http://localhost:5001";
const TENANT_SLUG = __ENV.TENANT_SLUG || "load-test-tenant";

const registrationErrors = new Rate("registration_errors");
const loginErrors = new Rate("login_errors");
const loginDuration = new Trend("login_duration_ms");

export const options = {
  stages: [
    { duration: "30s", target: 10 },
    { duration: "2m", target: 50 },
    { duration: "1m", target: 50 },
    { duration: "30s", target: 0 },
  ],
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<500"],
    login_errors: ["rate<0.01"],
    login_duration_ms: ["p(95)<300"],
  },
};

function randomEmail() {
  return `load-user-${Date.now()}-${Math.random().toString(36).slice(2)}@test.com`;
}

export default function () {
  const email = randomEmail();
  const password = "LoadTest1!";

  // Register
  const registerRes = http.post(
    `${BASE_URL}/v1/auth/register`,
    JSON.stringify({
      email,
      password,
      name: "Load Test User",
      tenantSlug: TENANT_SLUG,
    }),
    { headers: { "Content-Type": "application/json" } },
  );
  registrationErrors.add(
    registerRes.status !== 201 && registerRes.status !== 200,
  );

  if (registerRes.status !== 201 && registerRes.status !== 200) {
    sleep(1);
    return;
  }

  // Login
  const start = Date.now();
  const loginRes = http.post(
    `${BASE_URL}/v1/auth/login`,
    JSON.stringify({ email, password }),
    { headers: { "Content-Type": "application/json" } },
  );
  loginDuration.add(Date.now() - start);
  loginErrors.add(loginRes.status !== 200);

  check(loginRes, {
    "login succeeded": (r) => r.status === 200,
    "token present": (r) => {
      try {
        return !!JSON.parse(r.body).token;
      } catch {
        return false;
      }
    },
  });

  if (loginRes.status !== 200) {
    sleep(1);
    return;
  }

  const token = JSON.parse(loginRes.body).token;

  // List my bookings
  const bookingsRes = http.get(
    `${BASE_URL}/v1/customers/me/bookings?page=1&pageSize=10`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  check(bookingsRes, { "bookings list 200": (r) => r.status === 200 });

  sleep(1);
}
