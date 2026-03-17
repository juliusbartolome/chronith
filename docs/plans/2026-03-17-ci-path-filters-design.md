# CI Path Filters — Design Doc

**Date:** 2026-03-17
**Status:** Approved
**Scope:** `.github/workflows/ci.yml`

---

## Problem

The main `ci.yml` workflow has no path filters. Every push to `main` and every PR
targeting `main` triggers all 6 jobs — .NET tests, Docker build, Playwright E2E,
k6 load tests, benchmarks, and CodeQL — even when only docs, dashboard, or SDK
files changed. The secondary workflows (`dashboard-ci.yml`, `docs-ci.yml`,
`sdk-ci.yml`, `sdk-csharp-ci.yml`) already have path filters; `ci.yml` is the
only offender.

## Solution

Two-layer path filtering:

1. **Workflow-level `paths-ignore`** — skip `ci.yml` entirely for changes that
   are fully covered by other dedicated workflows (docs, dashboard, TS SDK).
2. **Job-level `dorny/paths-filter`** — a lightweight `changes` detection job
   sets boolean outputs; each downstream job adds an `if:` condition so it only
   runs when its relevant files changed.

## Layer 1 — Workflow-level `paths-ignore`

Added to both `on.push` and `on.pull_request`:

```yaml
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

This means `ci.yml` **never fires** for:

- Doc-only changes (plans, design docs, site docs, README, CHANGELOG, AGENTS.md)
- Dashboard-only changes (covered by `dashboard-ci.yml`)
- TypeScript SDK-only changes (covered by `sdk-ci.yml`)
- Workflow file changes for other pipelines

## Layer 2 — Job-level `dorny/paths-filter`

A new first job named `changes` uses `dorny/paths-filter@v3` to detect which
file groups were modified. Each downstream job depends on `changes` and checks
its output.

### Filter groups

| Filter       | Paths                                                                                                                                                                              | Consumers             |
| ------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------- |
| `backend`    | `src/**`, `tests/Chronith.Tests.Unit/**`, `tests/Chronith.Tests.Integration/**`, `tests/Chronith.Tests.Functional/**`, `Chronith.slnx`, `global.json`, `src/Directory.Build.props` | `dotnet-test`         |
| `docker`     | `src/**`, `Dockerfile`, `docker-compose*.yml`, `.dockerignore`, `docker/**`                                                                                                        | `docker-build`        |
| `e2e`        | `src/**`, `dashboard/**`, `Dockerfile`, `docker-compose*.yml`                                                                                                                      | `playwright-e2e`      |
| `load`       | `src/**`, `tests/Chronith.Tests.Load/**`, `Dockerfile`, `docker-compose*.yml`                                                                                                      | `k6-load-tests`       |
| `benchmarks` | `src/**`, `tests/Chronith.Tests.Performance/**`, `Chronith.slnx`, `global.json`                                                                                                    | `benchmarks`          |
| `csharp`     | `src/**`, `tests/**`, `Chronith.slnx`, `global.json`                                                                                                                               | `codeql (csharp)`     |
| `javascript` | `dashboard/**`, `packages/**`, `docs/site/**`                                                                                                                                      | `codeql (javascript)` |

### Job conditions

Each job gets a compound condition preserving any existing conditions:

```yaml
dotnet-test:
  needs: changes
  if: needs.changes.outputs.backend == 'true'

docker-build:
  needs: changes
  if: needs.changes.outputs.docker == 'true'

playwright-e2e:
  needs: [changes, docker-build]
  if: needs.changes.outputs.e2e == 'true'

k6-load-tests:
  needs: [changes, docker-build]
  if: needs.changes.outputs.load == 'true'

benchmarks:
  needs: changes
  if: >-
    needs.changes.outputs.benchmarks == 'true'
    && github.event_name == 'push'
    && github.ref == 'refs/heads/main'

codeql:
  needs: changes
  if: >-
    (github.event_name == 'pull_request' || (github.event_name == 'push' && github.ref == 'refs/heads/main'))
    && (needs.changes.outputs.csharp == 'true' || needs.changes.outputs.javascript == 'true')
```

### `dorny/paths-filter` push behavior

On `push` events, `dorny/paths-filter` compares against the previous commit by
default (`base: ''`). This is correct for our use case — we want to detect what
the merge commit introduced.

## Edge Cases

### Required status checks

No jobs in `ci.yml` are currently required status checks on `main` branch
protection (the old `check-target-branch` was removed during GitHub Flow
migration). If required checks are added later, skipped jobs will block merging.
The fix is to either:

- Not make filtered jobs required, or
- Add a `ci-gate` summary job that always runs and reports success when all
  applicable jobs pass (or are correctly skipped).

### Workflow file self-reference

Changes to `ci.yml` itself should trigger all jobs. `paths-ignore` does not
list `.github/workflows/ci.yml`, so a change to `ci.yml` will trigger the
workflow. The `changes` job filter should include a `workflow` filter for
`.github/workflows/ci.yml` that forces all outputs to `true`, or we rely on
the workflow-level trigger already handling this.

**Decision:** Add `.github/workflows/ci.yml` to every filter group in
`dorny/paths-filter`. This ensures any CI workflow change runs all jobs.

### `packages/sdk-csharp/**` changes

The C# SDK has its own `sdk-csharp-ci.yml`, but changes to `src/**` (which the
C# SDK depends on) are NOT in `paths-ignore`, so `ci.yml` will still fire for
backend changes that might affect the C# SDK. This is correct behavior.

## Non-goals

- Splitting `ci.yml` into separate workflow files per job (too much disruption
  for marginal benefit).
- Filtering `auto-approve.yml` (it's a 2-step lightweight workflow that should
  run on every PR).
- Modifying `dashboard-ci.yml`, `docs-ci.yml`, `sdk-ci.yml`, or
  `sdk-csharp-ci.yml` — they already have correct path filters.
