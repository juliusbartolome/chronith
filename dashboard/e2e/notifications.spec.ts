import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

test.describe("Notifications Dashboard", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("notifications page renders", async ({ page }) => {
    await page.goto("/notifications");
    await expect(page.locator("h1")).toContainText(/notification/i);
  });

  test("channel configuration cards are visible", async ({ page }) => {
    await page.goto("/notifications");
    const emailCard = page
      .locator('[data-testid="channel-email"], :has-text("Email")')
      .first();
    await expect(emailCard).toBeVisible({ timeout: 5000 });
  });
});
