import { test, expect } from "@playwright/test";

test.describe("Staff Management", () => {
  test("staff list page renders", async ({ page }) => {
    await page.goto("/staff");
    await expect(page.locator("h1")).toContainText(/staff/i);
  });

  test("new staff form is accessible", async ({ page }) => {
    await page.goto("/staff/new");
    await expect(
      page.locator("form, [data-testid='staff-form']"),
    ).toBeVisible();
  });
});
