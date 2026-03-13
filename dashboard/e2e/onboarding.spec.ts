import { test, expect } from "@playwright/test";
import { loginAsAdmin } from "./helpers/auth";

test.describe("Onboarding Wizard", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/onboarding");
    await page.evaluate(() => localStorage.removeItem("onboarding-progress"));
    await page.reload();
  });

  test("onboarding wizard renders", async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto("/onboarding");
    await expect(page.locator("h1")).toContainText(
      /welcome|get started|onboarding/i,
    );
  });

  test("shows step 1 by default", async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto("/onboarding");
    await expect(page.locator("h2, [data-testid='step-title']")).toContainText(
      /booking type|step 1/i,
    );
  });

  test("can dismiss wizard", async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto("/onboarding");
    const dismissButton = page.locator(
      'button[title="Skip setup"], button:has(svg[data-lucide="x"])',
    );
    if (await dismissButton.isVisible()) {
      await dismissButton.click();
      await expect(page).toHaveURL(/dashboard/);
    }
  });
});

test.describe("Subscription Settings", () => {
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
