import { Page } from "@playwright/test";

export async function loginAsAdmin(page: Page) {
  await page.goto("/login");
  await page.fill(
    '[name="email"]',
    process.env.TEST_ADMIN_EMAIL ?? "admin@test.com",
  );
  await page.fill(
    '[name="password"]',
    process.env.TEST_ADMIN_PASSWORD ?? "Password1!",
  );
  await page.click('[type="submit"]');
  await page.waitForURL(/\/(dashboard|onboarding)/);
}

export async function loginAsCustomer(
  page: Page,
  email: string,
  password: string,
) {
  await page.goto("/book/test-tenant/auth/login");
  await page.fill('[name="email"]', email);
  await page.fill('[name="password"]', password);
  await page.click('[type="submit"]');
  await page.waitForURL(/my-bookings|book/);
}
