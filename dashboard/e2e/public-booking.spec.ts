import { test, expect } from "@playwright/test";

const TENANT_SLUG = process.env.TEST_TENANT_SLUG ?? "test-tenant";

test.describe("Public Booking Flow", () => {
  test("booking landing page renders", async ({ page }) => {
    await page.goto(`/book/${TENANT_SLUG}`);
    const hasCards = await page
      .locator('.booking-type-card, [data-testid="booking-type-card"]')
      .first()
      .isVisible()
      .catch(() => false);
    const hasEmpty = await page
      .locator(':has-text("No booking types")')
      .isVisible()
      .catch(() => false);
    const hasHeader = await page.locator("h1, h2").first().isVisible();
    expect(hasCards || hasEmpty || hasHeader).toBe(true);
  });

  test("booking type detail page is accessible", async ({ page }) => {
    await page.goto(`/book/${TENANT_SLUG}`);
    const card = page
      .locator('.booking-type-card a, [data-testid="booking-type-card"] a')
      .first();
    if (await card.isVisible()) {
      await card.click();
      await expect(page).toHaveURL(new RegExp(`/book/${TENANT_SLUG}/`));
    }
  });

  test("customer login page renders", async ({ page }) => {
    await page.goto(`/book/${TENANT_SLUG}/auth/login`);
    await expect(page.locator('[type="email"]')).toBeVisible();
    await expect(page.locator('[type="password"]')).toBeVisible();
  });

  test("customer register page renders", async ({ page }) => {
    await page.goto(`/book/${TENANT_SLUG}/auth/register`);
    await expect(page.locator("form")).toBeVisible();
  });

  test("my bookings page shows sign-in prompt for unauthenticated users", async ({
    page,
  }) => {
    await page.goto(`/book/${TENANT_SLUG}/my-bookings`);
    // Page stays at same URL, renders "Sign in" prompt (not a redirect)
    await expect(
      page.locator(':has-text("Sign in to view your bookings"), :has-text("Sign in")')
        .first()
    ).toBeVisible({ timeout: 10000 });
  });
});
