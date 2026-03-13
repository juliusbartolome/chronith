import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

test.describe("Staff Management", () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

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
