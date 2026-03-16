import { test as setup, expect } from "@playwright/test";

const AUTH_FILE = "e2e/.auth/admin.json";

// Setup gets a longer timeout because it absorbs the cold-start latency of the
// Next.js standalone server on the very first request in CI.
setup.use({ actionTimeout: 30_000 });

setup("authenticate as admin", async ({ page }) => {
  await page.goto("/login", { waitUntil: "networkidle" });

  // Wait for the login form to hydrate before filling fields
  await page.waitForSelector('[name="tenantSlug"]', { timeout: 30_000 });

  await page.fill(
    '[name="tenantSlug"]',
    process.env.TEST_TENANT_SLUG ?? "test-tenant",
  );
  await page.fill(
    '[name="email"]',
    process.env.TEST_ADMIN_EMAIL ?? "admin@test.com",
  );
  await page.fill(
    '[name="password"]',
    process.env.TEST_ADMIN_PASSWORD ?? "Password1!",
  );
  await page.click('[type="submit"]');
  await page.waitForURL(/\/(bookings|onboarding)/, { timeout: 30_000 });
  await expect(page.locator("h1")).toBeVisible({ timeout: 10_000 });
  await page.context().storageState({ path: AUTH_FILE });
});
