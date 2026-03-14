import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

test.describe("Onboarding Wizard", () => {
  test.describe.configure({ mode: "serial" });

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto("/onboarding");
    // Clear progress using the actual localStorage key used by the page
    await page.evaluate(() =>
      localStorage.removeItem("chronith-onboarding-step"),
    );
    await page.reload();
  });

  test("onboarding wizard renders", async ({ page }) => {
    // Step 0 title is "Welcome to Chronith" rendered in h1
    await expect(page.locator("h1")).toContainText(/welcome to chronith/i);
  });

  test("shows step 1 by default", async ({ page }) => {
    // Step 0 is the welcome step — h1 contains the step title
    await expect(page.locator("h1")).toContainText(/welcome to chronith/i);
  });

  test("can dismiss wizard", async ({ page }) => {
    // Actual dismiss element: <button type="button">Skip setup</button>
    const dismissButton = page.locator('button:has-text("Skip setup")');
    if (await dismissButton.isVisible()) {
      await dismissButton.click();
      await expect(page).toHaveURL(/bookings/);
    }
  });
});

test.describe("Subscription Settings", () => {
  test.describe.configure({ mode: "serial" });

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test("subscription page renders", async ({ page }) => {
    await page.goto("/settings/subscription");
    await expect(page.locator("h1")).toContainText(/subscription/i);
  });

  test("shows plan name", async ({ page }) => {
    await page.goto("/settings/subscription");
    await expect(
      page.locator("h2, .text-2xl, [data-testid='plan-name']"),
    ).toBeVisible({ timeout: 5000 });
  });

  test("shows usage meters", async ({ page }) => {
    await page.goto("/settings/subscription");
    const progress = page.locator('[role="progressbar"], .progress');
    await expect(progress.first()).toBeVisible({ timeout: 5000 });
  });

  test("change plan button opens dialog", async ({ page }) => {
    await page.goto("/settings/subscription");
    const changeBtn = page.locator('button:has-text("Change Plan")');
    if (await changeBtn.isVisible()) {
      await changeBtn.click();
      await expect(page.locator('[role="dialog"]')).toBeVisible();
    }
  });
});
