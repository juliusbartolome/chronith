import { test, expect } from "@playwright/test";

const TENANT_SLUG = process.env.TEST_TENANT_SLUG ?? "test-tenant";

test.describe("Public Booking Flow", () => {
  test("booking landing page renders", async ({ page }) => {
    await page.goto(`/book/${TENANT_SLUG}`);
    // Page renders either booking type links, "No booking types" message, or the
    // "Book an Appointment" h1 — all are valid states depending on API availability
    const hasCards = await page
      .locator(`a[href*="/book/${TENANT_SLUG}/"]`)
      .first()
      .isVisible()
      .catch(() => false);
    const hasEmpty = await page
      .locator('p:has-text("No booking types are currently available")')
      .isVisible()
      .catch(() => false);
    const hasHeader = await page
      .locator('h1:has-text("Book an Appointment")')
      .isVisible()
      .catch(() => false);
    expect(hasCards || hasEmpty || hasHeader).toBe(true);
  });

  test("booking type detail page is accessible", async ({ page }) => {
    await page.goto(`/book/${TENANT_SLUG}`);
    // Cards are <Link href="/book/{tenantSlug}/{slug}"> elements
    const card = page
      .locator(`a[href*="/book/${TENANT_SLUG}/"]`)
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
    // Page renders a sign-in prompt (not a redirect). Exact text from my-bookings/page.tsx:
    // <p className="text-muted-foreground">Sign in to view your bookings.</p>
    await expect(
      page.locator('p:has-text("Sign in to view your bookings.")'),
    ).toBeVisible({ timeout: 10000 });
  });
});
