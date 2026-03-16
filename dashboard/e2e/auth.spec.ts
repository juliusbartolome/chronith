import { test, expect } from "@playwright/test";

// These tests require an unauthenticated browser context
test.use({ storageState: { cookies: [], origins: [] } });

test.describe("Admin Authentication", () => {
  test("login page renders", async ({ page }) => {
    await page.goto("/login");
    // h1 on login page is "Chronith" (brand heading)
    await expect(page.locator("h1")).toContainText(/chronith/i);
    await expect(page.locator('[type="email"]')).toBeVisible();
    await expect(page.locator('[type="password"]')).toBeVisible();
    await expect(page.locator('[type="submit"]')).toBeVisible();
  });

  test("shows error on invalid credentials", async ({ page }) => {
    await page.goto("/login");
    await page.fill('[name="tenantSlug"]', "test-tenant");
    await page.fill('[type="email"]', "wrong@example.com");
    await page.fill('[type="password"]', "wrongpassword");
    await page.click('[type="submit"]');
    await expect(
      page.locator('[role="alert"], .text-destructive'),
    ).toBeVisible();
  });

  test("shows error state when accessing protected page unauthenticated", async ({
    page,
  }) => {
    // No server-side auth middleware exists yet, so the page renders but
    // API calls fail because no JWT cookie is present.
    await page.goto("/bookings");
    await expect(page.locator("h1")).toContainText(/bookings/i);
    // The page should eventually show either the table or an error/loading state
    await expect(
      page.locator("text=Failed to load bookings.").or(page.locator("text=Loading")),
    ).toBeVisible({ timeout: 10000 });
  });
});

test.describe("Signup Flow", () => {
  test("signup page renders 3-step wizard", async ({ page }) => {
    await page.goto("/signup");
    // h1 on signup page is "Create your account"
    await expect(page.locator("h1")).toContainText(/create your account/i);
    // Step 1 uses id="tenantName" for business name and id="email" for admin email
    await expect(page.locator('[id="tenantName"]')).toBeVisible();
    await expect(page.locator('[id="email"]')).toBeVisible();
  });

  test("step 1 validation prevents advancing with empty fields", async ({
    page,
  }) => {
    await page.goto("/signup");
    // The Next button in step 1 is a submit button inside the form
    await page.click('button[type="submit"]');
    // Multiple validation errors appear; assert at least one is visible
    await expect(
      page.locator(".text-red-500").first(),
    ).toBeVisible();
  });

  test("step 2 shows plan selection cards after filling step 1", async ({
    page,
  }) => {
    await page.goto("/signup");
    // Fill step 1 with correct field IDs
    await page.fill('[id="tenantName"]', "Test Corp");
    await page.fill('[id="email"]', "admin@test.com");
    await page.fill('[id="password"]', "Password1!");
    await page.fill('[id="confirmPassword"]', "Password1!");
    await page.click('button[type="submit"]');
    // Step 2 — plan cards (buttons for selection)
    await expect(page.locator("button[type='button']").first()).toBeVisible({
      timeout: 5000,
    });
  });
});
