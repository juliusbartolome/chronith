import { b64encode } from "k6/encoding";
import { hmac } from "k6/crypto";

/**
 * Returns the base URL for the API under test.
 * Override with: k6 run -e BASE_URL=http://myhost:5000 script.js
 */
export function baseUrl() {
  return __ENV.BASE_URL || "http://localhost:5000";
}

const TENANT_ID = "00000000-0000-0000-0000-000000000001";
const SIGNING_KEY =
  __ENV.JWT_SIGNING_KEY || "load-test-signing-key-at-least-32-chars!!";

const ROLE_USER_MAP = {
  TenantAdmin: "user-admin-1",
  TenantStaff: "user-staff-1",
  Customer: "user-customer-1",
  TenantPaymentService: "user-paymentsvc-1",
};

/**
 * Builds a signed JWT for the given role using HMAC-SHA256 (RS256 is not needed
 * for load testing — the server validates with the same HS256 key).
 *
 * Returns the Authorization header object: { Authorization: "Bearer <token>" }
 */
export function authHeader(role) {
  const userId = ROLE_USER_MAP[role] || "user-unknown";

  const header = base64url(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const now = Math.floor(Date.now() / 1000);
  const payload = base64url(
    JSON.stringify({
      sub: userId,
      tenant_id: TENANT_ID,
      role: role,
      iat: now,
      exp: now + 3600,
    }),
  );

  const signingInput = `${header}.${payload}`;
  const signature = hmacSha256Base64url(SIGNING_KEY, signingInput);

  return {
    Authorization: `Bearer ${header}.${payload}.${signature}`,
  };
}

// ── Helpers ──────────────────────────────────────────────────────────────────

function base64url(str) {
  return b64encode(str, "rawurl");
}

function hmacSha256Base64url(secret, message) {
  const sig = hmac("sha256", secret, message, "base64");
  return sig.replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}
