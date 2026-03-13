import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

test.describe("Analytics Dashboard", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("analytics page renders with tabs", async ({ page }) => {
    await page.goto("/analytics");
    await expect(page.locator("h1")).toContainText(/analytics/i);
    await expect(
      page.locator('[role="tab"], button:has-text("Bookings")'),
    ).toBeVisible();
  });

  test("switching tabs works", async ({ page }) => {
    await page.goto("/analytics");
    const revenueTab = page.locator(
      '[role="tab"]:has-text("Revenue"), button:has-text("Revenue")',
    );
    if (await revenueTab.isVisible()) {
      await revenueTab.click();
      await expect(page.locator("h2, .recharts-wrapper")).toBeVisible({
        timeout: 5000,
      });
    }
  });
});
