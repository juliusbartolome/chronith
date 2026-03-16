import { test as setup, expect } from "@playwright/test";

const AUTH_FILE = "e2e/.auth/admin.json";

setup("authenticate as admin", async ({ page }) => {
  await page.goto("/login");
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
  await page.waitForURL(/\/(bookings|onboarding)/);
  await expect(page.locator("h1")).toBeVisible();
  await page.context().storageState({ path: AUTH_FILE });
});
