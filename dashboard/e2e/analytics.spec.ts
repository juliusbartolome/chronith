import { test, expect } from "@playwright/test";

test.describe("Analytics Dashboard", () => {
  test("analytics page renders with tabs", async ({ page }) => {
    await page.goto("/analytics");
    await expect(page.locator("h1")).toContainText(/analytics/i);
    await expect(
      page.getByRole("tab", { name: "Bookings" }),
    ).toBeVisible();
  });

  test("switching tabs works", async ({ page }) => {
    await page.goto("/analytics");
    const revenueTab = page.getByRole("tab", { name: "Revenue" });
    await expect(revenueTab).toBeVisible();
    await revenueTab.click();
    await expect(revenueTab).toHaveAttribute("aria-selected", "true");
  });
});
