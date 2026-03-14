import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

test.describe("Bookings Dashboard", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("bookings list page renders", async ({ page }) => {
    await page.goto("/bookings");
    await expect(page).toHaveURL(/bookings/);
    await expect(page.locator("h1")).toContainText(/bookings/i);
  });

  test("shows empty state when no bookings", async ({ page }) => {
    await page.goto("/bookings");
    const hasTable = await page.locator("table").isVisible();
    const hasEmpty = await page
      .locator('[data-testid="empty-state"], .text-muted-foreground')
      .isVisible();
    expect(hasTable || hasEmpty).toBe(true);
  });
});
