# Fix Playwright E2E Tests — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix all 28-36 failing Playwright E2E tests in CI by addressing 5 root causes: missing seed data, broken login flow (missing tenantSlug), wrong waitForURL regex, missing Next.js middleware, and missing public booking test data.

**Architecture:** The dashboard is a Next.js 16 app (client-side rendered, `"use client"`) that proxies API calls through BFF routes to the .NET backend. Auth uses httpOnly cookies set by the BFF. The backend is strictly multi-tenant — all login requires a `tenantSlug`. The Playwright tests run in CI against a docker-compose stack (API + Postgres + Redis) with the dashboard built and served via `npm start`.

**Tech Stack:** Next.js 16, Playwright, FastEndpoints, PostgreSQL, BCrypt, Docker Compose, GitHub Actions

**Branch:** Create `fix/playwright-e2e` from `develop` (worktree at `.worktrees/v1.0`).

---

## Root Cause Summary

| #   | Severity | Issue                                                                                   | Fix                                                                       |
| --- | -------- | --------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| RC1 | CRITICAL | No seed data in CI `playwright-e2e` job                                                 | Add SQL INSERT step for tenant, admin user, booking types, availability   |
| RC2 | CRITICAL | Dashboard login form doesn't send `tenantSlug` (backend requires it)                    | Add tenant slug field to login form + update BFF route                    |
| RC3 | CRITICAL | `loginAsAdmin` waits for `/(dashboard\|onboarding)/` but login redirects to `/bookings` | Fix regex to `/(bookings\|onboarding)/`                                   |
| RC4 | MEDIUM   | `dashboard/src/proxy.ts` has auth guard but isn't wired as Next.js middleware           | Move to `dashboard/src/middleware.ts`, rename export, update PUBLIC_PATHS |
| RC5 | MEDIUM   | Public booking tests need tenant + booking type data                                    | Covered by RC1 seed data                                                  |

---

### Task 1: Add `tenantSlug` to dashboard login form

The backend `POST /v1/auth/login` requires `TenantSlug` (validated NotEmpty by FluentValidation). The dashboard currently sends only `{email, password}`.

**Files:**

- Modify: `dashboard/src/app/(auth)/login/page.tsx`

**Step 1: Add tenantSlug to the login form schema and UI**

In `dashboard/src/app/(auth)/login/page.tsx`, update the Zod schema, form, and UI:

```tsx
const schema = z.object({
  tenantSlug: z.string().min(1, "Workspace slug is required"),
  email: z.string().email("Invalid email"),
  password: z.string().min(1, "Password is required"),
});
```

Add the field to the form JSX (before the email field):

```tsx
<div>
  <Label htmlFor="tenantSlug">Workspace</Label>
  <Input
    id="tenantSlug"
    type="text"
    autoComplete="organization"
    placeholder="your-workspace"
    {...register("tenantSlug")}
    aria-describedby={errors.tenantSlug ? "slug-error" : undefined}
  />
  {errors.tenantSlug && (
    <p id="slug-error" className="mt-1 text-xs text-red-600">
      {errors.tenantSlug.message}
    </p>
  )}
</div>
```

The `onSubmit` already does `JSON.stringify(data)` which will now include `tenantSlug`. The BFF at `/api/auth/login` passes through the body verbatim to `POST /v1/auth/login`. The backend accepts `TenantSlug` as a property name (FastEndpoints model binding is case-insensitive for JSON).

**Step 2: Verify the change builds**

Run: `npm run build` in `dashboard/`
Expected: Build succeeds with no type errors.

**Step 3: Commit**

```bash
git add dashboard/src/app/\(auth\)/login/page.tsx
git commit -m "fix(dashboard): add tenantSlug field to admin login form

The backend POST /v1/auth/login requires TenantSlug but the login form
only sent email and password, causing a 400 validation error on every
login attempt."
```

---

### Task 2: Wire up Next.js middleware for auth redirects

`dashboard/src/proxy.ts` contains auth guard logic but is NOT registered as Next.js middleware. It needs to be at `dashboard/src/middleware.ts` with the export named `middleware`.

**Files:**

- Delete: `dashboard/src/proxy.ts`
- Create: `dashboard/src/middleware.ts`

**Step 1: Create the middleware file**

Create `dashboard/src/middleware.ts` with the following content (adapted from `proxy.ts`):

```typescript
import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

const PUBLIC_PATHS = ["/login", "/signup", "/book", "/api/auth", "/api/public"];

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Allow public paths
  if (PUBLIC_PATHS.some((p) => pathname.startsWith(p))) {
    return NextResponse.next();
  }

  const token = request.cookies.get("chronith-auth")?.value;
  if (!token) {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("from", pathname);
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
```

Key changes from `proxy.ts`:

- Export name: `proxy` → `middleware` (required by Next.js)
- PUBLIC_PATHS: Added `/signup`, `/book` (public booking pages), `/api/public` (public API proxy routes)
- Removed redundant `/_next` and `/favicon` checks (already excluded by `config.matcher`)

**Step 2: Delete the old proxy.ts**

Delete `dashboard/src/proxy.ts`.

**Step 3: Verify the change builds**

Run: `npm run build` in `dashboard/`
Expected: Build succeeds. Middleware is automatically picked up by Next.js from `src/middleware.ts`.

**Step 4: Commit**

```bash
git add dashboard/src/middleware.ts
git rm dashboard/src/proxy.ts
git commit -m "fix(dashboard): wire up Next.js middleware for auth redirects

The auth guard logic in proxy.ts was never registered as Next.js
middleware because the file was at the wrong path with the wrong
export name. Moved to src/middleware.ts, renamed export to 'middleware',
and added /signup, /book, /api/public to PUBLIC_PATHS."
```

---

### Task 3: Fix `loginAsAdmin` waitForURL regex

**Files:**

- Modify: `dashboard/e2e/helpers/auth.ts`

**Step 1: Fix the regex**

In `dashboard/e2e/helpers/auth.ts`, line 14:

Change:

```typescript
await page.waitForURL(/\/(dashboard|onboarding)/);
```

To:

```typescript
await page.waitForURL(/\/(bookings|onboarding)/);
```

Also update `loginAsAdmin` to fill in the tenantSlug field (added in Task 1):

```typescript
export async function loginAsAdmin(page: Page) {
  await page.goto("/login");
  await page.fill(
    '[name="tenantSlug"]',
    process.env.TEST_TENANT_SLUG ?? "test-tenant",
  );
  await page.fill(
    '[name="email"]',
    process.env.TEST_ADMIN_EMAIL ?? "admin@test.com",
  );
  await page.fill(
    '[name="password"]',
    process.env.TEST_ADMIN_PASSWORD ?? "Password1!",
  );
  await page.click('[type="submit"]');
  await page.waitForURL(/\/(bookings|onboarding)/);
}
```

**Step 2: Commit**

```bash
git add dashboard/e2e/helpers/auth.ts
git commit -m "fix(e2e): fix loginAsAdmin to send tenantSlug and wait for /bookings

loginAsAdmin was missing the tenantSlug field (backend rejects without it)
and waitForURL matched /(dashboard|onboarding)/ but login redirects to
/bookings. Fixed both issues."
```

---

### Task 4: Fix auth.spec.ts redirect test

**Files:**

- Modify: `dashboard/e2e/auth.spec.ts`

**Step 1: Update the redirect test**

Now that middleware is wired up (Task 2), the test "redirects unauthenticated users to /login" at line 18 should work. However, verify the assertion matches the middleware behavior. The middleware redirects to `/login?from=/bookings`, so `page.toHaveURL(/login/)` will match.

No change needed to this test — it should now pass with middleware in place.

**Step 2: Verify signup form field IDs match**

Check the signup test at line 29: `page.locator('[id="tenantName"]')`. The actual signup form has `<Input id="tenantName" ...>` — this matches. The heading `<h1>Create your account</h1>` also matches. No changes needed.

**Step 3: Commit (if any changes were made)**

If no changes are needed, skip this commit.

---

### Task 5: Add seed data to `playwright-e2e` CI job

**Files:**

- Modify: `.github/workflows/ci.yml`

**Step 1: Add seed database step to the playwright-e2e job**

Add a "Seed database" step after "Wait for API health" and before "Setup Node". Model it after the k6 seed step.

The seed must include:

1. A tenant (`test-tenant`)
2. An admin user with a real BCrypt hash for `Password1!`
3. At least one booking type with availability windows (for public booking tests)

```yaml
- name: Seed database
  run: |
    docker exec -i chronith-postgres-1 psql -U chronith -d chronith <<'SQL'
      SET search_path = chronith;

      -- Tenant
      INSERT INTO tenants ("Id", "Name", "Slug", "TimeZoneId", "IsDeleted", "CreatedAt")
      VALUES ('00000000-0000-0000-0000-000000000001','Test Tenant','test-tenant','UTC',false,NOW())
      ON CONFLICT ("Id") DO NOTHING;

      -- Admin user (password: Password1!)
      INSERT INTO "TenantUsers" ("Id", "TenantId", "Email", "PasswordHash", "Role", "IsActive", "IsEmailVerified", "CreatedAt")
      VALUES (
        '00000000-0000-0000-0000-000000000099',
        '00000000-0000-0000-0000-000000000001',
        'admin@test.com',
        '$2b$12$Qj5PqpaFuFx64GY2FVgjSuoz/MGmZdR6qwLqhFSkIX4092S/m0Eh2',
        'Owner',
        true,
        true,
        NOW()
      )
      ON CONFLICT ("Id") DO NOTHING;

      -- Booking types
      INSERT INTO booking_types ("Id", "TenantId", "Name", "Slug", "DurationMinutes", "Capacity", "Kind", "PaymentMode", "PriceInCentavos", "IsDeleted")
      VALUES
        ('00000000-0000-0000-0000-000000000010','00000000-0000-0000-0000-000000000001','Consultation','consultation',60,10,'TimeSlot','Manual',50000,false),
        ('00000000-0000-0000-0000-000000000011','00000000-0000-0000-0000-000000000001','Quick Check','quick-check',30,5,'TimeSlot','Manual',0,false)
      ON CONFLICT ("Id") DO NOTHING;

      -- Availability windows (Mon-Sun, 08:00-18:00 for both booking types)
      INSERT INTO availability_windows ("Id", "BookingTypeId", "DayOfWeek", "StartTime", "EndTime")
      SELECT gen_random_uuid(), bt."Id", d.day, '08:00'::time, '18:00'::time
      FROM booking_types bt
      CROSS JOIN (SELECT generate_series(0,6) AS day) d
      WHERE bt."Id" IN (
        '00000000-0000-0000-0000-000000000010',
        '00000000-0000-0000-0000-000000000011'
      )
      ON CONFLICT DO NOTHING;
    SQL
```

Insert this step at line ~140 (after "Wait for API health", before "Setup Node").

**Step 2: Add timeout-minutes to the playwright-e2e job**

Add `timeout-minutes: 15` to the `playwright-e2e` job to prevent runaway timeouts from consuming CI minutes:

```yaml
playwright-e2e:
  name: Playwright E2E
  runs-on: ubuntu-latest
  needs: docker-build
  timeout-minutes: 15
```

**Step 3: Verify CI YAML is valid**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"`
Expected: No errors.

**Step 4: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "fix(ci): add seed data and timeout for Playwright E2E job

The playwright-e2e CI job had no seed data, so no tenant, users, or
booking types existed. Every test that required login or tenant data
would time out after 30s. Added SQL seed step matching the k6 pattern,
and a 15-minute job timeout to prevent runaway CI."
```

---

### Task 6: Local verification

**Step 1: Build the full solution**

Run: `dotnet build Chronith.slnx` from the repo root.
Expected: Build succeeds.

**Step 2: Build the dashboard**

Run: `npm run build` in `dashboard/`
Expected: Build succeeds with middleware detected.

**Step 3: Run Playwright tests locally (if Docker is available)**

Start the stack:

```bash
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d
```

Seed the database (copy the SQL from Task 5 and run via `docker exec`).

Build and start the dashboard:

```bash
cd dashboard && npm run build && PORT=3000 CHRONITH_API_URL=http://localhost:5001 npm start &
```

Run Playwright:

```bash
cd dashboard && PLAYWRIGHT_BASE_URL=http://localhost:3000 CI=true npx playwright test --project="Desktop Chrome"
```

Expected: All tests pass (or at minimum, no timeouts — some tests may still need minor adjustments if the UI rendering differs from expectations).

**Step 4: Commit any remaining fixes discovered during local testing**

---

### Task 7: Merge to develop

```bash
# Ensure on fix/playwright-e2e branch in .worktrees/v1.0
git checkout develop
git merge --no-ff fix/playwright-e2e -m "fix: merge Playwright E2E test fixes into develop"
git branch -d fix/playwright-e2e
```

---

## File Change Summary

| File                                      | Action                           | Task |
| ----------------------------------------- | -------------------------------- | ---- |
| `dashboard/src/app/(auth)/login/page.tsx` | Modify — add `tenantSlug` field  | 1    |
| `dashboard/src/proxy.ts`                  | Delete                           | 2    |
| `dashboard/src/middleware.ts`             | Create — auth middleware         | 2    |
| `dashboard/e2e/helpers/auth.ts`           | Modify — fix `loginAsAdmin`      | 3    |
| `.github/workflows/ci.yml`                | Modify — add seed data + timeout | 5    |

## Notes

- The BCrypt hash `$2b$12$Qj5PqpaFuFx64GY2FVgjSuoz/MGmZdR6qwLqhFSkIX4092S/m0Eh2` is for password `Password1!` with work factor 12. This is a test-only value — not a security concern since it's for CI seed data.
- The `TenantUsers` table does NOT have an `IsDeleted` column (unlike most other entities), so no soft-delete filter needs to be considered.
- The `TenantUsers` table uses PascalCase (no snake_case) because `ToTable("TenantUsers")` is explicit in the configuration, though it lives in the `chronith` schema.
- The `auth.spec.ts` tests should pass without modification once middleware + seed data are in place. Monitor during local testing.
- Public booking tests (`public-booking.spec.ts`) are already written defensively with fallback assertions — they should pass once tenant + booking type seed data exists.
