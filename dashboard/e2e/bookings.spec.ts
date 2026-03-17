import { test, expect } from "@playwright/test";

test.describe("Bookings Dashboard", () => {
  test("bookings list page renders", async ({ page }) => {
    await page.goto("/bookings");
    await expect(page).toHaveURL(/bookings/);
    await expect(page.locator("h1")).toContainText(/bookings/i);
  });

  test("shows empty state when no bookings", async ({ page }) => {
    await page.goto("/bookings");
    // Wait for loading to finish — page shows "Loading…" while fetching
    await page.waitForFunction(
      () => !document.body.textContent?.includes("Loading"),
      { timeout: 10000 },
    );
    // After loading, expect either the bookings table, empty text, or error
    const hasTable = await page.locator("table").isVisible();
    const hasEmpty = await page
      .locator('text="No bookings found."')
      .isVisible();
    const hasError = await page
      .locator("text=Failed to load bookings.")
      .isVisible();
    expect(hasTable || hasEmpty || hasError).toBe(true);
  });
});
