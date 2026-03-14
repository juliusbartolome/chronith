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
    // ChannelCard renders <div class="rounded-lg border p-6"><h3>Email</h3>...</div>
    // Target the h3 channel label inside a border card to avoid matching nav/headings
    const emailCard = page.locator('.rounded-lg.border h3:has-text("Email")');
    await expect(emailCard).toBeVisible({ timeout: 5000 });
  });
});
