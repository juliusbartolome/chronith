# CI Path Filters Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add path-based filtering to `ci.yml` so jobs only run when their relevant files change.

**Architecture:** Two-layer approach — workflow-level `paths-ignore` to skip entirely for docs/dashboard/SDK-TS changes, plus `dorny/paths-filter@v3` in a `changes` job for per-job granularity.

**Tech Stack:** GitHub Actions, `dorny/paths-filter@v3`

**Design doc:** `docs/plans/2026-03-17-ci-path-filters-design.md`

---

### Task 1: Add `paths-ignore` to workflow triggers

**Files:**

- Modify: `.github/workflows/ci.yml:3-7`

**Step 1: Add `paths-ignore` to both trigger blocks**

Change:

```yaml
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
```

To:

```yaml
on:
  push:
    branches: [main]
    paths-ignore:
      - "docs/**"
      - "*.md"
      - "dashboard/**"
      - "packages/sdk-typescript/**"
      - ".github/workflows/dashboard-ci.yml"
      - ".github/workflows/docs-ci.yml"
      - ".github/workflows/sdk-ci.yml"
      - ".github/workflows/auto-approve.yml"
  pull_request:
    branches: [main]
    paths-ignore:
      - "docs/**"
      - "*.md"
      - "dashboard/**"
      - "packages/sdk-typescript/**"
      - ".github/workflows/dashboard-ci.yml"
      - ".github/workflows/docs-ci.yml"
      - ".github/workflows/sdk-ci.yml"
      - ".github/workflows/auto-approve.yml"
```

**Step 2: Validate YAML syntax**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"`
Expected: No output (valid YAML)

**Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add paths-ignore to ci.yml workflow triggers"
```

---

### Task 2: Add `changes` detection job with `dorny/paths-filter`

**Files:**

- Modify: `.github/workflows/ci.yml` (insert new job after `env:` block, before `dotnet-test`)

**Step 1: Add the `changes` job**

Insert after the `env:` block (after line 14) and before the existing `jobs:` key. The new `changes` job becomes the first job under `jobs:`:

```yaml
changes:
  name: Detect Changes
  runs-on: ubuntu-latest
  permissions:
    contents: read
    pull-requests: read
  outputs:
    backend: ${{ steps.filter.outputs.backend }}
    docker: ${{ steps.filter.outputs.docker }}
    e2e: ${{ steps.filter.outputs.e2e }}
    load: ${{ steps.filter.outputs.load }}
    benchmarks: ${{ steps.filter.outputs.benchmarks }}
    csharp: ${{ steps.filter.outputs.csharp }}
    javascript: ${{ steps.filter.outputs.javascript }}
  steps:
    - uses: actions/checkout@v6

    - uses: dorny/paths-filter@v3
      id: filter
      with:
        filters: |
          backend:
            - 'src/**'
            - 'tests/Chronith.Tests.Unit/**'
            - 'tests/Chronith.Tests.Integration/**'
            - 'tests/Chronith.Tests.Functional/**'
            - 'Chronith.slnx'
            - 'global.json'
            - 'src/Directory.Build.props'
            - '.github/workflows/ci.yml'
          docker:
            - 'src/**'
            - 'Dockerfile'
            - 'docker-compose*.yml'
            - '.dockerignore'
            - 'docker/**'
            - '.github/workflows/ci.yml'
          e2e:
            - 'src/**'
            - 'dashboard/**'
            - 'Dockerfile'
            - 'docker-compose*.yml'
            - '.github/workflows/ci.yml'
          load:
            - 'src/**'
            - 'tests/Chronith.Tests.Load/**'
            - 'Dockerfile'
            - 'docker-compose*.yml'
            - '.github/workflows/ci.yml'
          benchmarks:
            - 'src/**'
            - 'tests/Chronith.Tests.Performance/**'
            - 'Chronith.slnx'
            - 'global.json'
            - '.github/workflows/ci.yml'
          csharp:
            - 'src/**'
            - 'tests/**'
            - 'Chronith.slnx'
            - 'global.json'
            - '.github/workflows/ci.yml'
          javascript:
            - 'dashboard/**'
            - 'packages/**'
            - 'docs/site/**'
            - '.github/workflows/ci.yml'
```

**Step 2: Validate YAML syntax**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"`
Expected: No output (valid YAML)

**Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add dorny/paths-filter changes detection job"
```

---

### Task 3: Add `needs` and `if` conditions to all downstream jobs

**Files:**

- Modify: `.github/workflows/ci.yml` — each of the 6 existing jobs

**Step 1: Update `dotnet-test`**

Add after the job name line:

```yaml
dotnet-test:
  name: .NET Tests
  needs: changes
  if: needs.changes.outputs.backend == 'true'
```

**Step 2: Update `docker-build`**

```yaml
docker-build:
  name: Docker Build
  needs: changes
  if: needs.changes.outputs.docker == 'true'
```

**Step 3: Update `playwright-e2e`**

Replace `needs: docker-build` with:

```yaml
playwright-e2e:
  name: Playwright E2E
  needs: [changes, docker-build]
  if: needs.changes.outputs.e2e == 'true'
```

**Step 4: Update `k6-load-tests`**

Replace `needs: docker-build` with:

```yaml
k6-load-tests:
  name: k6 Load Tests
  needs: [changes, docker-build]
  if: needs.changes.outputs.load == 'true'
```

**Step 5: Update `benchmarks`**

Replace the existing `if:` with a compound condition:

```yaml
benchmarks:
  name: Benchmarks
  needs: changes
  if: >-
    needs.changes.outputs.benchmarks == 'true'
    && github.event_name == 'push'
    && github.ref == 'refs/heads/main'
```

**Step 6: Update `codeql`**

Replace the existing `if:` with a compound condition:

```yaml
codeql:
  name: CodeQL (${{ matrix.language }})
  needs: changes
  if: >-
    (github.event_name == 'pull_request' || (github.event_name == 'push' && github.ref == 'refs/heads/main'))
    && (needs.changes.outputs.csharp == 'true' || needs.changes.outputs.javascript == 'true')
```

**Step 7: Validate YAML syntax**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"`
Expected: No output (valid YAML)

**Step 8: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add path-based job conditions to all ci.yml jobs"
```

---

### Task 4: Handle `playwright-e2e` and `k6-load-tests` dependency on skipped `docker-build`

**Important:** When `docker-build` is skipped (because `needs.changes.outputs.docker != 'true'`), downstream jobs that `needs: [changes, docker-build]` will also be skipped — even if their own condition would be true. This happens because GitHub Actions treats a skipped `needs` dependency as a failure by default.

**Files:**

- Modify: `.github/workflows/ci.yml` — `playwright-e2e` and `k6-load-tests` jobs

**Step 1: Verify the dependency issue**

Both `playwright-e2e` and `k6-load-tests` currently use `docker-build` only for cache warmth — they rebuild the image themselves with `load: true`. The `needs: docker-build` was for ordering, not artifact passing. However, if we remove the `needs` dependency, they'll build in parallel (which is fine — they each use `cache-from: type=gha`).

**Decision:** Remove `docker-build` from the `needs` array of `playwright-e2e` and `k6-load-tests`. They each build their own image with `load: true` and `cache-from: type=gha`, so the dependency was only an optimization for cache priming. With path filters, both will only run when relevant files change anyway.

Update:

```yaml
playwright-e2e:
  name: Playwright E2E
  needs: changes
  if: needs.changes.outputs.e2e == 'true'

k6-load-tests:
  name: k6 Load Tests
  needs: changes
  if: needs.changes.outputs.load == 'true'
```

**Step 2: Validate YAML syntax**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"`
Expected: No output (valid YAML)

**Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: decouple e2e and k6 jobs from docker-build dependency"
```

---

### Task 5: Verify full workflow file

**Step 1: Validate final YAML**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"`
Expected: No output

**Step 2: Verify job dependency graph**

Manually verify that the `changes` job has no `needs`, and all other jobs have `needs: changes` (or `needs: [changes]`). Verify no circular dependencies.

**Step 3: Dry-run with `act` (optional, if available)**

Run: `act pull_request --workflows .github/workflows/ci.yml --dryrun`
Expected: Shows job execution plan with conditions

**Step 4: Commit design + plan docs**

```bash
git add docs/plans/2026-03-17-ci-path-filters-design.md docs/plans/2026-03-17-ci-path-filters-plan.md
git commit -m "docs: add CI path filters design and implementation plan"
```
