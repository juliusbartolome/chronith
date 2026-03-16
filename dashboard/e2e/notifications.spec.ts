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

  test("channels tab is active and content loads", async ({ page }) => {
    await page.goto("/notifications");
    // The Channels tab should be selected by default
    const channelsTab = page.getByRole("tab", { name: "Channels" });
    await expect(channelsTab).toBeVisible();
    await expect(channelsTab).toHaveAttribute("aria-selected", "true");
    // Templates tab should also exist
    await expect(page.getByRole("tab", { name: "Templates" })).toBeVisible();
  });
});
